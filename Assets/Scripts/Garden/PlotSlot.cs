using System;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;

namespace PlantBreeding.Garden
{
    /// <summary>
    /// Чиста логіка (без UI) однієї грядки. GardenManager створює слоти,
    /// відновлює їх зі збереження і оновлює в Update/тикеру.
    /// PlotSlotView (в Assets/Scripts/UI) підписується на подію StateChanged
    /// і перемальовує відповідну клітинку.
    ///
    /// Увесь ріст рахується від ігрового часу (GameClock), тому рослина
    /// росте й коли гра закрита — основа щоденного повернення (docs/ECONOMY.md).
    /// Хвороба/шкідник (PlantAilments) ставить ріст на паузу до лікування.
    /// </summary>
    [Serializable]
    public class PlotSlot
    {
        public int slotIndex;
        public PlotState state = PlotState.Empty;
        public PlantData plant;
        public PotData pot;
        public StarterBoostKind boost = StarterBoostKind.None;
        public DateTime plantedAtUtc;

        /// <summary>Час росту (сек) з урахуванням горщика/добрива, зафіксований у момент посадки.</summary>
        public float effectiveGrowTimeSeconds;

        /// <summary>Останній полив (або посадка) — від нього рахується наступна спрага.</summary>
        public DateTime lastWateredUtc;
        /// <summary>Скільки разів полито вчасно — кожен дає бонус до ціни продажу (EconomyConfig).</summary>
        public int wateredCount;
        /// <summary>Прискорення за кристали вже використане для цієї посадки.</summary>
        public bool speedUpUsed;

        /// <summary>Запланована (або поточна, якщо Sick) хвороба/шкідник.</summary>
        public AilmentKind ailment = AilmentKind.None;
        /// <summary>На якій частці росту з'явиться хвороба; &lt;0 — не заплановано/вже вилікувано.</summary>
        public float ailmentAtFraction = -1f;
        /// <summary>Момент, коли рослина захворіла (ріст «заморожений» на ньому).</summary>
        public DateTime sickSinceUtc;
        /// <summary>Шкідник: скільки тапів лишилось.</summary>
        public int pestTapsLeft;

        public event Action<PlotSlot> StateChanged;

        public PlotSlot(int index, bool unlocked)
        {
            slotIndex = index;
            state = unlocked ? PlotState.Empty : PlotState.Locked;
        }

        public void Plant(PlantData plantData, PotData potData, StarterBoostKind boostKind)
        {
            if (state != PlotState.Empty) return;
            plant = plantData;
            pot = potData;
            boost = boostKind;

            var forecast = PlantingForecast.Compute(plantData, potData, boostKind);
            effectiveGrowTimeSeconds = forecast.growTimeSeconds;

            plantedAtUtc = GameClock.UtcNow;
            lastWateredUtc = plantedAtUtc;
            wateredCount = 0;
            speedUpUsed = false;
            (ailment, ailmentAtFraction) = PlantAilments.RollAtPlanting(plantData, potData, effectiveGrowTimeSeconds);
            pestTapsLeft = 0;
            SetState(PlotState.Growing);
            EconomyService.Track(DailyTaskKind.Plant, 1, plantData.plantId);
        }

        /// <summary>Відкриває заблоковану грядку (рівень або покупка).</summary>
        public void Unlock()
        {
            if (state == PlotState.Locked) SetState(PlotState.Empty);
        }

        private bool HasPendingAilment => ailment != AilmentKind.None && ailmentAtFraction >= 0f && state != PlotState.Sick;

        /// <summary>Інтервал спраги з урахуванням горщика.</summary>
        private double WaterIntervalSeconds =>
            plant.waterDepleteSeconds * (1f + (pot != null ? pot.waterIntervalBonus : 0f));

        /// <summary>Викликається періодично GardenManager'ом (напр. раз на секунду).</summary>
        public void Tick()
        {
            if ((state != PlotState.Growing && state != PlotState.NeedsWater) || plant == null) return;

            DateTime now = GameClock.UtcNow;
            double elapsed = (now - plantedAtUtc).TotalSeconds;

            // Хвороба перевіряється раніше за дозрівання: якщо гра була закрита,
            // рослина «захворіла» саме в запланований момент і відтоді не росла.
            if (HasPendingAilment)
            {
                double at = ailmentAtFraction * effectiveGrowTimeSeconds;
                if (elapsed >= at)
                {
                    sickSinceUtc = plantedAtUtc.AddSeconds(at);
                    pestTapsLeft = PlantAilments.Get(ailment).isPest ? PlantAilments.PestTaps(ailment) : 0;
                    SetState(PlotState.Sick);
                    return;
                }
            }

            if (elapsed >= effectiveGrowTimeSeconds)
            {
                SetState(PlotState.Ready);
                return;
            }

            // Спрага не зупиняє ріст — лише пропонує полив за бонус до ціни.
            if (state == PlotState.Growing
                && wateredCount < EconomyConfig.MaxCareWaterings
                && (now - lastWateredUtc).TotalSeconds >= WaterIntervalSeconds)
            {
                SetState(PlotState.NeedsWater);
            }
        }

        /// <summary>
        /// Знімає частку (0..1) часу, що лишився: зсуває момент посадки назад,
        /// тож прогрес і таймер одразу стрибають уперед. Ціну/обмеження
        /// перевіряє EconomyService.TrySpeedUp. Запланована хвороба, яку
        /// прискорення «перестрибнуло», скасовується — гравець платив не за неї.
        /// </summary>
        public void SpeedUp(float fraction)
        {
            double remaining = GetRemainingSeconds();
            if (remaining <= 0 || plant == null) return;
            plantedAtUtc -= TimeSpan.FromSeconds(remaining * Mathf.Clamp01(fraction));
            speedUpUsed = true;
            if (HasPendingAilment
                && (GameClock.UtcNow - plantedAtUtc).TotalSeconds >= ailmentAtFraction * effectiveGrowTimeSeconds)
            {
                ClearAilment();
            }
            StateChanged?.Invoke(this); // стан той самий — але треба перемалювати й зберегти
            Tick();
        }

        public void Water()
        {
            if (state != PlotState.NeedsWater) return;
            wateredCount++;
            lastWateredUtc = GameClock.UtcNow;
            SetState(PlotState.Growing);
            GameManager.Instance?.AddXp(EconomyConfig.WaterXp);
            EconomyService.Track(DailyTaskKind.Water, 1);
            GameEvents.RaiseToast($"Полито! +{Mathf.RoundToInt(EconomyConfig.CareBonusPerWatering * 100f)}% до ціни врожаю");
        }

        // ── Хвороби/шкідники ─────────────────────────────────────────────
        public AilmentDef CurrentAilment => PlantAilments.Get(ailment);

        /// <summary>Один тап по шкіднику. Повертає true, якщо шкідника прибрано.</summary>
        public bool TapPest()
        {
            if (state != PlotState.Sick || !CurrentAilment.isPest) return false;
            pestTapsLeft = Mathf.Max(0, pestTapsLeft - 1);
            if (pestTapsLeft > 0)
            {
                StateChanged?.Invoke(this); // оновити лічильник у чіпі
                return false;
            }
            Cure();
            return true;
        }

        /// <summary>
        /// Знімає хворобу і продовжує ріст з того ж місця: час хвороби
        /// додається до моменту посадки (і поливу), тож прогрес не стрибає.
        /// Оплату засобу перевіряє EconomyService.TryCureWithRemedy.
        /// </summary>
        public void Cure()
        {
            if (state != PlotState.Sick) return;
            TimeSpan paused = GameClock.UtcNow - sickSinceUtc;
            if (paused > TimeSpan.Zero)
            {
                plantedAtUtc += paused;
                lastWateredUtc += paused;
            }
            ClearAilment();
            SetState(PlotState.Growing);
            EconomyService.Track(DailyTaskKind.Cure, 1);
            Tick();
        }

        private void ClearAilment()
        {
            ailment = AilmentKind.None;
            ailmentAtFraction = -1f;
            pestTapsLeft = 0;
        }

        /// <returns>Монети за продаж (0, якщо збирати нічого).</returns>
        /// <param name="silent">Без тосту про продаж — «Зібрати все» показує один підсумок.</param>
        public int Harvest(bool silent = false)
        {
            if (state != PlotState.Ready) return 0;
            var harvested = plant;
            var harvestedPot = pot;
            int watered = wateredCount;
            plant = null;
            pot = null;
            boost = StarterBoostKind.None;
            wateredCount = 0;
            speedUpUsed = false;
            ClearAilment();
            SetState(PlotState.Empty);
            return EconomyService.GrantHarvest(harvested, harvestedPot, watered, silent);
        }

        /// <summary>Скільки секунд росту вже зараховано (під час хвороби — заморожено).</summary>
        private double ElapsedGrowSeconds
        {
            get
            {
                DateTime now = state == PlotState.Sick ? sickSinceUtc : GameClock.UtcNow;
                return (now - plantedAtUtc).TotalSeconds;
            }
        }

        public float GetGrowthProgress01()
        {
            if (plant == null) return 0f;
            return Mathf.Clamp01((float)(ElapsedGrowSeconds / effectiveGrowTimeSeconds));
        }

        public double GetRemainingSeconds()
        {
            if (plant == null) return 0;
            return Math.Max(0, effectiveGrowTimeSeconds - ElapsedGrowSeconds);
        }

        /// <summary>Коли хвороба з'явиться (для push-сповіщень); null — не заплановано.</summary>
        public DateTime? PendingAilmentAtUtc =>
            HasPendingAilment ? plantedAtUtc.AddSeconds(ailmentAtFraction * effectiveGrowTimeSeconds) : (DateTime?)null;

        // ── Збереження ───────────────────────────────────────────────────
        public PlotSaveData ToSave()
        {
            return new PlotSaveData
            {
                slotIndex = slotIndex,
                state = state.ToString(),
                plantId = plant != null ? plant.plantId : "",
                plantedAtUnixSeconds = ToUnix(plantedAtUtc),
                unlocked = state != PlotState.Locked,
                potId = pot != null ? pot.potId : "",
                boost = (int)boost,
                effectiveGrowTimeSeconds = effectiveGrowTimeSeconds,
                lastWateredUnixSeconds = ToUnix(lastWateredUtc),
                wateredCount = wateredCount,
                speedUpUsed = speedUpUsed,
                ailment = (int)ailment,
                ailmentAtFraction = ailmentAtFraction,
                sickSinceUnixSeconds = ToUnix(sickSinceUtc),
                pestTapsLeft = pestTapsLeft,
            };
        }

        /// <summary>
        /// Відновлює посаджену рослину зі збереження (без подій і без трекінгу
        /// завдань). Порожні/заблоковані записи ігноруються — стан відкритості
        /// визначає EconomyService.IsPlotUnlocked, а не збереження.
        /// </summary>
        public void RestoreFrom(PlotSaveData save)
        {
            if (state == PlotState.Locked || save == null || string.IsNullOrEmpty(save.plantId)) return;
            var savedPlant = PlantCatalog.Get(save.plantId);
            if (savedPlant == null) return;

            plant = savedPlant;
            pot = string.IsNullOrEmpty(save.potId) ? null : Resources.Load<PotData>("Pots/" + save.potId);
            boost = Enum.IsDefined(typeof(StarterBoostKind), save.boost) ? (StarterBoostKind)save.boost : StarterBoostKind.None;
            plantedAtUtc = FromUnix(save.plantedAtUnixSeconds);
            effectiveGrowTimeSeconds = save.effectiveGrowTimeSeconds > 0 ? save.effectiveGrowTimeSeconds : savedPlant.growTimeSeconds;
            lastWateredUtc = save.lastWateredUnixSeconds > 0 ? FromUnix(save.lastWateredUnixSeconds) : plantedAtUtc;
            wateredCount = save.wateredCount;
            speedUpUsed = save.speedUpUsed;
            ailment = Enum.IsDefined(typeof(AilmentKind), save.ailment) ? (AilmentKind)save.ailment : AilmentKind.None;
            ailmentAtFraction = ailment == AilmentKind.None ? -1f : save.ailmentAtFraction;
            sickSinceUtc = FromUnix(save.sickSinceUnixSeconds);
            pestTapsLeft = save.pestTapsLeft;

            state = Enum.TryParse(save.state, out PlotState saved) && saved != PlotState.Empty && saved != PlotState.Locked
                ? saved
                : PlotState.Growing;
            if (state == PlotState.Sick && ailment == AilmentKind.None) state = PlotState.Growing;
        }

        private static double ToUnix(DateTime utc) =>
            utc == default ? 0 : (utc - DateTime.UnixEpoch).TotalSeconds;

        private static DateTime FromUnix(double seconds) => DateTime.UnixEpoch.AddSeconds(seconds);

        private void SetState(PlotState newState)
        {
            if (state == newState) return;
            state = newState;
            StateChanged?.Invoke(this);
            GameEvents.RaisePlotStateChanged(slotIndex);
        }
    }
}

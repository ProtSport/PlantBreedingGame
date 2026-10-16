# Лагодить збірку .aab для Google Play (Tools -> Build Android App Bundle).
# Запускати ВІД ІМЕНІ АДМІНІСТРАТОРА, коли Unity закрита:
#   правий клік на «Windows PowerShell» -> «Запуск від імені адміністратора», потім:
#   powershell -ExecutionPolicy Bypass -File "C:\Users\andri\Local Sites\PlantBreedingGame_Unity_Starter\PlantBreedingGame\Tools\fix-package-cache.ps1"
#
# 1) Вмикає довгі шляхи Windows (>260 символів) — без них частина файлів пакетів недоступна.
# 2) Повертає доступ до кешу пакетів Unity і видаляє заблоковану копію пакета
#    render-pipelines.core — Unity сама завантажить її заново при наступному відкритті.

$ErrorActionPreference = 'Continue'
$project = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $project 'Library\PackageCache'

if (Get-Process Unity -ErrorAction SilentlyContinue) {
    Write-Host 'Спершу закрий Unity Editor, потім запусти скрипт ще раз.' -ForegroundColor Yellow
    exit 1
}

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host 'Потрібні права адміністратора: запусти PowerShell «від імені адміністратора».' -ForegroundColor Yellow
    exit 1
}

Write-Host '1) Вмикаю довгі шляхи Windows...'
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled -Value 1 -PropertyType DWORD -Force | Out-Null
Write-Host '   LongPathsEnabled = 1'

Write-Host '2) Повертаю доступ до кешу пакетів...'
$long = '\\?\' + $cache
takeown /f $long /r /d y | Out-Null
icacls $long /reset /t /c /q | Out-Null
attrib -r -s -h "$cache\*" /s /d 2>$null

Write-Host '3) Видаляю заблокований пакет render-pipelines.core (Unity завантажить його заново)...'
Get-ChildItem -LiteralPath $cache -Directory -Filter 'com.unity.render-pipelines.core@*' | ForEach-Object {
    $path = '\\?\' + $_.FullName
    cmd /c "rmdir /s /q `"$path`"" 2>&1 | Out-Null
    if (Test-Path -LiteralPath $_.FullName) {
        Write-Host "   НЕ вдалося видалити $($_.Name)." -ForegroundColor Red
        Write-Host '   Імовірно, файли блокує антивірус ESET: відкрий ESET -> Інструменти -> Карантин / Файли журналу,' -ForegroundColor Red
        Write-Host '   додай виняток для папки проєкту і запусти скрипт ще раз.' -ForegroundColor Red
    } else {
        Write-Host "   Видалено $($_.Name)"
    }
}

Write-Host ''
Write-Host 'Готово. Перезавантаж комп''ютер (щоб довгі шляхи увімкнулись для всіх програм) і напиши мені —' -ForegroundColor Green
Write-Host 'я ще раз запущу збірку .aab.' -ForegroundColor Green

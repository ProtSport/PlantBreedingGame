# Завантажує Firebase Unity SDK і External Dependency Manager у GooglePackages/ (поза git:
# один файл пакета App — 111 МБ, а GitHub не приймає файли понад 100 МБ).
# Packages/manifest.json посилається на них як file:../GooglePackages/<назва>.
#
# Запуск із кореня проєкту (Unity закрита):
#   powershell -ExecutionPolicy Bypass -File Tools\fetch-google-packages.ps1
#
# Версії — ті, з якими зібрано гру (2026-10-10). Щоб оновити, зміни $FirebaseVersion / $EdmVersion
# (список: https://developers.google.com/unity/archive) і запусти скрипт з -Force.

param([switch]$Force)

$ErrorActionPreference = 'Stop'
$FirebaseVersion = '13.17.0'
$EdmVersion = '1.2.186'
$Registry = 'https://dl.google.com/games/registry/unity'

$project = Split-Path -Parent $PSScriptRoot
$target = Join-Path $project 'GooglePackages'
New-Item -ItemType Directory -Force $target | Out-Null

# папка в GooglePackages -> пакет і версія
$packages = [ordered]@{
    'external-dependency-manager' = @('com.google.external-dependency-manager', $EdmVersion)
    'app'         = @('com.google.firebase.app', $FirebaseVersion)
    'auth'        = @('com.google.firebase.auth', $FirebaseVersion)
    'firestore'   = @('com.google.firebase.firestore', $FirebaseVersion)
    'analytics'   = @('com.google.firebase.analytics', $FirebaseVersion)
    'crashlytics' = @('com.google.firebase.crashlytics', $FirebaseVersion)
}

foreach ($folder in $packages.Keys) {
    $name, $version = $packages[$folder]
    $dest = Join-Path $target $folder
    if ((Test-Path (Join-Path $dest 'package.json')) -and -not $Force) {
        Write-Host "  $folder — вже є"
        continue
    }
    $tgz = Join-Path $env:TEMP "$name-$version.tgz"
    Write-Host "  $folder — завантажую $name $version..."
    Invoke-WebRequest -Uri "$Registry/$name/$name-$version.tgz" -OutFile $tgz -UseBasicParsing
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    New-Item -ItemType Directory -Force $dest | Out-Null
    tar -xzf $tgz -C $dest --strip-components=1   # tar є у Windows 10+
    Remove-Item $tgz
}

Write-Host ''
Write-Host 'Готово. Відкрий проєкт у Unity — пакети підхопляться з GooglePackages/.' -ForegroundColor Green

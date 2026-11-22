# Выпуск ClickForge

## Подготовка

1. Сверьте `Version` в [Directory.Build.props](Directory.Build.props) и `MyAppVersion` в [installer/ClickForge.iss](installer/ClickForge.iss).
2. Из корня проекта выполните сборку и тесты. Перед публикацией проверьте установку обеих версий рядом и запуск с ярлыков. Сборка Release использует нейтральный путь к исходникам и не включает отладочные символы. Перед открытой публикацией проверьте готовые файлы на локальные пути и секреты.

```powershell
dotnet build ClickForge.sln -c Release
dotnet run --project tests/ClickForge.Tests -c Release
dotnet publish src/ClickForge.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish-full
dotnet publish src/ClickForge.Lite -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish-lite
```

3. Соберите оба установщика с помощью Inno Setup 6. В PowerShell, где доступен `ISCC.exe`:

```powershell
$full = (Resolve-Path artifacts/publish-full).Path
$lite = (Resolve-Path artifacts/publish-lite).Path
ISCC.exe "/DPublishDir=$full" installer/ClickForge.iss
ISCC.exe /DLiteEdition=1 "/DPublishDir=$lite" installer/ClickForge.iss
```

## Черновик и публикация

1. Откройте [ClickForge Releases](https://github.com/Slake1301/ClickForge/releases) и создайте **Draft a new release**.
2. Укажите тег вида `v0.2.19`, выберите проверенный коммит в публичном репозитории, добавьте описание изменений и прикрепите оба установщика из `artifacts/installer`.
3. Сохраните черновик и проверьте содержимое. **Publish release** нажимает владелец после проверки.

Публичная история содержит исходники приложения. В неё не должны попадать личные контактные данные, конфигурация или история обращений бота, локальные пути и ссылки на приватные коммиты. Полная версия 0.2.19 и новее проверяет релизы этого репозитория. При переходе со старого канала установите 0.2.19 вручную.

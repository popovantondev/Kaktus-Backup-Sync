# Quelltextpaket · Source package · Исходники

## Deutsch

`tools/Export-Source.ps1` erstellt aus einem geprüften, sauberen lokalen Commit ein
ZIP und eine entpackte Kopie. Es enthält keine Git-Historie, Aufgaben, Berichte,
Arbeitsnotizen oder private Daten. Die vom Eigentümer zur Weitergabe freigegebene
Startaufnahme ist enthalten. Das freigegebene Maskottchenbild gehört
zum öffentlichen Quelltext; private Generierungsmetadaten sind entfernt. Die bestehende
lokale Historie bleibt unverändert. Dieses Paket als Grundlage eines neuen öffentlichen Repositorys nutzen.

Zum Bauen Windows und das SDK aus `global.json` verwenden. Für Benutzer enthält die
portable Ausgabe bereits ihre Laufzeit. LICENSE beschreibt die aktuellen Bedingungen;
es ist keine Open-Source-Lizenz. `RIGHTS.md` erläutert die erlaubte persönliche
Nutzung eines unveränderten Release-Binaries in drei Sprachen. Laufzeit- und
Drittanbieterhinweise bleiben im ausführbaren Paket.

Git speichert lokale Entwicklungsstände. Push sendet sie an GitHub. Vor dem ersten
Push einen neuen leeren Repository-Verlauf verwenden und die Identität des Commit-
Autors prüfen. Ein öffentlicher Benutzername und Profil sind separat im Konto sichtbar.

## English

Build and verify a source snapshot:

```powershell
dotnet test --project tests/AntonsBackupManager.Core.Tests -c Release
dotnet build tests/AntonsBackupManager.UiCheck -c Release -o runtime/ui-check
dotnet runtime/ui-check/AntonsBackupManager.UiCheck.dll runtime/ui-audit
dotnet runtime/ui-check/AntonsBackupManager.UiCheck.dll --crash-check
./tools/Publish.ps1
./tools/Test-Package.ps1
```

The export includes the approved raster mascot with its original image pixels,
but without private generation metadata. It includes the startup recording;
the owner confirmed permission to redistribute it. The recording remains optional
at runtime. See
the included ASSET-NOTICES.md, LICENSE, RIGHTS.md, and THIRD_PARTY_NOTICES.md.
The private package containing that recording
is not a public release artifact.

Windows CI builds, tests file operations, runs windowless workflows and checks recovery
after terminating its own test child. Configuration follows the official
[checkout](https://github.com/actions/checkout) and
[setup-dotnet](https://github.com/actions/setup-dotnet) documentation.
CI has not run on GitHub until this snapshot is published. Full visual/tray checks
stay in the local WPF harness. The workflow does not publish packages.

## Русский

Для GitHub подготовлен отдельный снимок без старой истории с персональными данными
автора, задач, журналов и приватных рабочих записей. В нём то же утверждённое
изображение маскота с исходными пикселями, но без приватных метаданных генерации.
Предоставленная стартовая запись включена с разрешения владельца. Исходный репозиторий сохранён.
Не загружайте вместо снимка его папку `.git` или используемую папку `runtime`.

Распакуйте снимок и проверьте сборку. Готовой программе ничего доустанавливать не нужно;
SDK нужен разработчику. Commit сохраняет код на компьютере. Push отправляет его на
сервер — это отдельное действие, которое здесь не выполнялось. Перед ним проверьте
имя и почту автора Git; для приватности можно использовать свой адрес GitHub noreply.
Права на исходники, использование готового бинарного выпуска и сторонние
компоненты описаны в `LICENSE`, `RIGHTS.md` и `THIRD_PARTY_NOTICES.md`.

Автоматическая проверка для GitHub подготовлена, но пока не запускалась на сервере.
Текущая лицензия оставляет права за правообладателем; открытый доступ к коду сам по
себе не означает разрешение свободно его использовать.

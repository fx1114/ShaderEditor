# Copilot Instructions

## Projektrichtlinien
- Bei UI-Anpassungen soll eine bestehende Textanzeige durch einen Graphen im MainWindow ersetzt werden, nicht zusätzlich daneben oder als separates Eingabefeld.
- Bei UI-Anpassungen sollen bestehende Buttons und ihre bisherige Funktion erhalten bleiben; neue Funktionen werden als zusätzliche Buttons ergänzt, nicht durch Umdeuten vorhandener Buttons.

## Testprojektstruktur
- Das Repository verwendet für das Testprojekt den solution-relativen Ordner `Tests`; die Projektdatei liegt unter `Tests/Tests.csproj` und Testdateien unter `Tests/`. 
- Nicht `ShaderEditor.Tests/ShaderEditor.Tests.csproj` verwenden.
- Wenn der Benutzende Tests für eine Klasse anfordert und die zugehörige Testdatei im solution-relativen Ordner `Tests` nicht existiert, soll eine neue Testdatei dort erstellt werden. Vor dem Antworten soll geprüft werden, ob die Testdatei existiert; vorhandene Testdateien sollen erweitert statt ersetzt werden.
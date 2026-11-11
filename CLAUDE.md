# Notes for Claude

- Code, names (classes, methods, files, folders) and comments are in **English**.
  Conversations and explanations with the project owner are in **French**.
- UI texts live in `Languages/*.json` and are read with `Texts.Get` / `Texts.Format` (or
  `{local:Tr key}` in XAML): never write a user-facing text directly in the code. English
  (`en.json`) is the reference and the fallback; keep `fr.json` and `en.json` in step (same keys).
- WPF on .NET 8, SQLite through Microsoft.Data.Sqlite. No new dependency without asking.
- Everything must be usable with the gaze (Tobii PCEye 5 + Prunel): big buttons, no small
  menus, scrolling always doubled by buttons. See the specification (cahier des charges).
- One step per branch and pull request. The owner compiles and tests on Windows: always say what
  was not compiled or not tested, and never promise untested behaviour.

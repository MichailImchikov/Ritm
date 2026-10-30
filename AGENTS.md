# Инструкции проекта

При обновлении приложения не создавайте новый ярлык или отдельную копию для запуска. Перед сборкой закройте работающее приложение «Ритм» и обновите штатную версию, чтобы существующий ярлык запускал обновлённое приложение.

## Commit messages

Write commit titles and descriptions in English using Conventional Commits: `type(scope): summary`; scope is optional. Use `feat` for new functionality, `fix` for bug fixes, `docs` for documentation, `style` for formatting, `refactor` for code restructuring, `perf` for performance, `test` for tests, `build` for build tooling, `ci` for CI configuration, and `chore` for maintenance. Use `!` before the colon or a `BREAKING CHANGE:` footer for breaking changes. Add a body after a blank line when useful. Reference: https://www.conventionalcommits.org/en/v1.0.0/.

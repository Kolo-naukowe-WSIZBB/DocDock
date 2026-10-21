# frontend

Interfejs użytkownika.

## Co tu trafia

- ekrany, komponenty i style z projektu graficznego (`docs/design/`)
- komunikacja z resztą tylko przez usługę aplikacji z `contracts/` (w wariancie TypeScript: przez lokalne API)

## Czego tu nie ma

- bezpośredniego dostępu do bazy
- bezpośredniego dostępu do plików na dysku

## Może korzystać z

Tylko z `contracts/`.

## Układ folderu

Zależy od wybranej technologii (Avalonia albo TypeScript). Decyzja trafi do `docs/decisions/`.

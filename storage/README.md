# storage

Pliki dokumentów na dysku.

## Co tu trafia

- import pliku do magazynu i liczenie hasha
- lokalna baza SQLite `docdock.sqlite`, utworzona z migracji w `database/`
- otwieranie pliku, „pokaż w folderze” i usuwanie do kosza aplikacji
- w etapie 2: wyciąganie tekstu z PDF-ów i dokumentów Worda

## Czego tu nie ma

- zapisu do bazy. Bazę i magazyn łączy `app/`.
- migracji bazy danych; znajdują się w `database/migrations/`.

## Może korzystać z

Tylko z `contracts/`.

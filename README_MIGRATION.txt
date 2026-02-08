# Instrucciones de Ejecución / Execution Instructions

## 1. Importar Backup a MariaDB (Restaurar) / Import Backup
Para cargar toda la información del archivo `full_database_backup.sql` a tu base de datos MariaDB:

1.  Abre una terminal.
2.  Ejecuta el siguiente comando:
    ```bash
    mysql -u root -p OCPP-MC < full_database_backup.sql
    ```
3.  Ingresa tu contraseña de MariaDB cuando se te solicite (por defecto en tu configuración: `root`).

*Nota: Si prefieres no escribir la contraseña, puedes usar:*
`mysql -u root -proot OCPP-MC < full_database_backup.sql`
*(Cuidado: Esto deja la contraseña visible en el historial).*

## 2. Generar Nuevo Backup desde SQLite / Generate New Backup
Si haces cambios en SQLite y quieres volver a generar el archivo SQL para MariaDB:

1.  Ejecuta el script de Python:
    ```bash
    python3 sqlite_to_mariadb_dumper.py OCPP.Core.sqlite full_database_backup.sql
    ```

## 3. Compilar para Linux (Opcional) / Build for Linux
Para generar los binarios de publicación:
```bash
./build_linux.sh
```

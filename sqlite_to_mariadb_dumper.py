import sqlite3
import sys

def quote_identifier(s):
    return f"`{s}`"

def escape_string(s):
    if s is None:
        return "NULL"
    # Basic escaping for SQL values
    return "'" + str(s).replace("'", "''").replace("\\", "\\\\") + "'"

def dump_table(cursor, table_name):
    # Get columns
    cursor.execute(f"PRAGMA table_info({quote_identifier(table_name)})")
    columns = [row[1] for row in cursor.fetchall()]
    
    query = f"SELECT * FROM {quote_identifier(table_name)}"
    cursor.execute(query)
    
    rows = cursor.fetchall()
    if not rows:
        return []

    statements = []
    statements.append(f"-- Data for table: {table_name}")
    
    col_names = ", ".join(quote_identifier(c) for c in columns)
    
    for row in rows:
        values = []
        for val in row:
            if val is None:
                values.append("NULL")
            elif isinstance(val, (int, float)):
                values.append(str(val))
            elif isinstance(val, bytes):
                # Handle blobs if necessary, often best skipped or hex encoded
                values.append(f"X'{val.hex()}'")
            else:
                 # Boolean in SQLite is 0/1, compatible with MariaDB/MySQL boolean
                values.append(escape_string(val))
        
        val_str = ", ".join(values)
        statements.append(f"INSERT IGNORE INTO {quote_identifier(table_name)} ({col_names}) VALUES ({val_str});")
        
    return statements

def main(db_path, output_file):
    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        
        # Get all tables
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table';")
        tables = [row[0] for row in cursor.fetchall()]
        
        excluded_tables = {'sqlite_sequence', '__EFMigrationsHistory'}
        
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write("-- MariaDB Full Dump generated from SQLite\n")
            f.write("-- Date: " + sqlite3.datetime.datetime.now().isoformat() + "\n")
            f.write("SET FOREIGN_KEY_CHECKS = 0;\n")
            f.write("SET SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO';\n")
            f.write("START TRANSACTION;\n\n")
            
            for table in tables:
                if table in excluded_tables:
                    continue
                
                print(f"Processing table: {table}")
                statements = dump_table(cursor, table)
                if statements:
                    f.write("\n".join(statements) + "\n\n")
            
            f.write("COMMIT;\n")
            f.write("SET FOREIGN_KEY_CHECKS = 1;\n")
            
        print(f"Successfully exported data to {output_file}")
        
    except Exception as e:
        print(f"Error: {e}")
    finally:
        if conn:
            conn.close()

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python3 sqlite_to_mariadb_dumper.py <sqlite_db_path> <output_sql_file>")
    else:
        main(sys.argv[1], sys.argv[2])

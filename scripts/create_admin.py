#!/usr/bin/env python3
"""One-time administrator bootstrap using Docker and ASP.NET Identity V3 hashing."""

import argparse
import base64
import getpass
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
import uuid


def password_hash(password):
    """Encode the ASP.NET Core Identity V3 SHA512 password hash format."""
    salt = os.urandom(16)
    key = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, 100_000, 32)
    return base64.b64encode(b"\x01" + struct.pack(">III", 2, 100_000, len(salt)) + salt + key).decode("ascii")


def validate(email, password):
    """Require an ASCII email and a strong bootstrap password."""
    if len(email) > 256 or not re.fullmatch(r"[A-Za-z0-9._+%-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", email):
        raise ValueError("Enter a valid ASCII email address.")
    if (len(password) < 12 or not re.search(r"[A-Z]", password)
            or not re.search(r"[a-z]", password) or not re.search(r"[0-9]", password)):
        raise ValueError("Use at least 12 characters with uppercase, lowercase, and a digit.")


def sql_literal(value):
    return "'" + value.replace("'", "''") + "'"


def build_sql(email, password):
    """Create a new Admin atomically, refusing duplicate accounts or missing roles."""
    validate(email, password)
    user_id = sql_literal(str(uuid.uuid4()))
    email_sql = sql_literal(email)
    normalized = sql_literal(email.upper())
    hashed = sql_literal(password_hash(password))
    security_stamp = sql_literal(uuid.uuid4().hex)
    concurrency_stamp = sql_literal(str(uuid.uuid4()))
    return f'''
SET standard_conforming_strings = on;
SET lock_timeout = '15s';
LOCK TABLE auth."Users", auth."UserRoles", auth."Roles" IN SHARE ROW EXCLUSIVE MODE;
DO $bootstrap$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM auth."Roles" WHERE "Name" = 'Admin') THEN
        RAISE EXCEPTION 'Admin role is missing. Start the API to run migrations and role seeding first.';
    END IF;
    IF EXISTS (SELECT 1 FROM auth."Users"
               WHERE "NormalizedEmail" = {normalized} OR "NormalizedUserName" = {normalized}) THEN
        RAISE EXCEPTION 'Account already exists. No password or permissions were changed.';
    END IF;
END $bootstrap$;
INSERT INTO auth."Users" (
    "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
    "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed",
    "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "HasAvatar"
) VALUES (
    {user_id}, {email_sql}, {normalized}, {email_sql}, {normalized}, true,
    {hashed}, {security_stamp}, {concurrency_stamp}, false, false, true, 0, false
);
INSERT INTO auth."UserRoles" ("UserId", "RoleId")
SELECT {user_id}, "Id" FROM auth."Roles" WHERE "Name" = 'Admin';
'''


def find_container():
    """Auto-select only when exactly one PostgreSQL container is running."""
    result = subprocess.check_output(
        ["docker", "ps", "--format", "{{json .}}"], text=True,
    )
    containers = [json.loads(line) for line in result.splitlines() if line]
    matches = [c["Names"] for c in containers
               if re.search(r"(^|/)(postgres|postgis)(:|/|$)", c["Image"])]
    if len(matches) != 1:
        raise ValueError("Could not select exactly one PostgreSQL container. Use --container NAME.")
    return matches[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--container", help="Running PostgreSQL container name, auto-detected if unique")
    parser.add_argument("--database", default="postgres", help="Application database, default: postgres")
    args = parser.parse_args()
    container = args.container or find_container()
    print(f"Target: {container}, database: {args.database}")
    email = input("New admin email: ").strip()
    password = getpass.getpass("New admin password, 12+ characters: ")
    if password != getpass.getpass("Confirm password: "):
        raise ValueError("Passwords do not match.")
    sql = build_sql(email, password)
    print("This creates an Admin account with confirmed email, not a Superuser.")
    if input("Type CREATE to continue: ").strip() != "CREATE":
        print("Cancelled. No changes made.")
        return
    # Resolve database credentials inside the container, not in process arguments.
    command = '''set -eu
if [ -n "${POSTGRES_PASSWORD_FILE:-}" ]; then
    PGPASSWORD="$(cat "$POSTGRES_PASSWORD_FILE")"
else
    PGPASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is not configured}"
fi
export PGPASSWORD
exec psql -X -v ON_ERROR_STOP=1 --single-transaction -U "${POSTGRES_USER:-postgres}" -d "$1"
'''
    subprocess.run(
        ["docker", "exec", "-i", container, "sh", "-c", command, "sh", args.database],
        input=sql, text=True, check=True, capture_output=True,
    )
    print(f"Admin created: {email}. Sign in at https://matrictrend.app/login")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, subprocess.CalledProcessError, FileNotFoundError) as error:
        # Do not echo subprocess output: SQL errors can contain password hashes.
        message = ("Database command failed; transaction rolled back. Check database, role, and account existence."
                   if isinstance(error, subprocess.CalledProcessError) else str(error))
        print("Error: " + message, file=sys.stderr)
        sys.exit(1)
    except (KeyboardInterrupt, EOFError):
        print("\nCancelled.", file=sys.stderr)
        sys.exit(1)

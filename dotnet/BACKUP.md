# ProphetOps backups and restore

Everything the agency has recorded — bookings, expenses, packages, users — lives in one
file, `prophetops.db`. The application copies that file automatically. This page explains
where the copies go and, if today is a bad day, exactly how to put one back.

You do not need to be a developer to follow this. Take it one step at a time.

## What happens on its own

| | |
| --- | --- |
| **What is copied** | `prophetops.db`, the whole database |
| **Where copies land** | `backups\` next to the application, e.g. `C:\ProphetOps\publish\backups\` |
| **How often** | Once a few minutes after the service starts, then every 24 hours |
| **How many are kept** | The newest 7. Older ones are deleted automatically |
| **File names** | `prophetops-20260719T021500Z.db` — the date and time (UTC) the copy was taken |

Names sort in date order, so the **last file in the list is the newest**.

Copies are taken with SQLite's own backup command rather than an ordinary file copy,
because the database is in use while the office works. Each finished copy is then opened
and checked before it is kept; a copy that does not pass is deleted and noted in the log,
so a bad file never sits in the folder pretending to be a good one.

A failed backup never stops the application. It is written to the log and tried again next
time.

## Read this before you need it

**The backups sit on the same PC and the same disk as the live database.** They protect you
from a wrong deletion, a bad import, or a corrupted database. They do **not** protect you
from that PC being stolen, burnt, or having its disk fail — that would take the backups too.

Once a week, copy the `backups\` folder onto a USB drive or a cloud folder and keep it
somewhere else. It takes a minute and it is the difference between losing a day and losing
the business.

## Check the backups are really running

Open `C:\ProphetOps\publish\backups\` in File Explorer and look at the dates. You should see
up to seven files, the newest less than a day old. That is the whole check.

If the newest file is old, or the folder is missing, the service has not been running — see
the log with `Get-EventLog -LogName Application -Source ProphetOps -Newest 20`, or run the
application from a terminal to watch it work.

## Restore a backup

This replaces everything currently in the system with the contents of the backup you pick.
**Anything entered after that backup was taken will be gone** — with daily backups that can
be up to a day of bookings. Only do this if the current database is lost or wrong.

Open PowerShell **as Administrator** and work through these in order.

**1. Stop the service.** Nothing can replace the database while it is open.

```powershell
sc.exe stop ProphetOps
sc.exe query ProphetOps
```

Wait until `sc.exe query` prints `STATE : 1 STOPPED`. If it still says `STOP_PENDING`, wait
a few seconds and run the query again.

**2. Choose the backup you want.** Usually the newest.

```powershell
cd C:\ProphetOps\publish
Get-ChildItem backups\prophetops-*.db | Sort-Object Name
```

Copy the file name of the one you want from the bottom of that list.

**3. Set the current database aside.** Do not delete it — if the restore goes wrong you will
want it back, and it may hold recent work.

```powershell
Rename-Item prophetops.db "prophetops-replaced-$(Get-Date -Format yyyyMMdd-HHmmss).db"
Remove-Item prophetops.db-wal, prophetops.db-shm -ErrorAction SilentlyContinue
```

The `-wal` and `-shm` files are scratch files belonging to the old database. They must go,
or SQLite may try to apply them to the restored one.

> If `Rename-Item` says the file is in use, the service has not stopped yet. Go back to
> step 1.

**4. Put the backup in place.** Use the name you copied in step 2.

```powershell
Copy-Item backups\prophetops-20260719T021500Z.db prophetops.db
```

Use `Copy-Item`, not `Move-Item` — leave the backup where it is.

**5. Start the service.**

```powershell
sc.exe start ProphetOps
```

**6. Check it worked.** Open `http://localhost:5099` in a browser, sign in, and look at
Bookings. The records should match the day the backup was taken. If they do, you are done —
the `prophetops-replaced-*.db` file from step 3 can be deleted once you are confident, or
kept somewhere safe.

## If the restore did not work

Nothing is lost yet. Stop the service again, delete the `prophetops.db` you just restored,
rename your `prophetops-replaced-*.db` back to `prophetops.db`, and start the service. That
puts you back exactly where you began, and you can try an older backup instead.

## Changing the schedule

Both settings live in `appsettings.Production.json` next to the application. Edit, save, and
restart the service.

```json
{
  "Backup": {
    "IntervalHours": 24,
    "Keep": 7
  }
}
```

`IntervalHours` is how long to wait between copies; `Keep` is how many to hold before the
oldest is deleted. Keeping more costs disk space — each copy is roughly the size of the
database.

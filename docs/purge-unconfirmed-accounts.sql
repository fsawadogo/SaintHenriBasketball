/* =============================================================================================
   Purge the unconfirmed accounts left by the September 2026 registration flood.

   Mirrors UnconfirmedAccountPurgeService / UnconfirmedAccountRepository exactly, plus one guard
   the service is missing (see step 0). Run the steps in order. Nothing is deleted before step 3,
   and step 3 rolls itself back unless you change one line.

   Window, matching the endpoint's parameters:
     olderThanDays = 2      never sweep something that signed up this morning; mail takes time
     createdAfter  = 2026-09-15   the flood. Franck Guiraud and Philip Wheeler signed up in March
                                  and April and are genuine, so the window keeps them out.

   CreatedOn is stored UTC, so every comparison here is UTC.
   ============================================================================================= */

DECLARE @CreatedAfter datetime2 = '2026-09-15T00:00:00';
DECLARE @Cutoff       datetime2 = DATEADD(day, -2, GETUTCDATE());


/* ---------------------------------------------------------------------------------------------
   STEP 0 — What actually references Users on this database.

   Read this before anything else. The two rows that come back as NO_ACTION are Payments and
   AccountCredits: a DELETE against a user holding either will fail rather than cascade, which is
   deliberate — both are financial records. Everything else cascades.

   This matters because the service's own history check looks at Payments, SessionRegistrations,
   SessionAttendances and Waitlists but NOT AccountCredits. An account holding only a credit
   passes that check and then fails on the FK. Step 2 below closes that gap; the API endpoint
   still has it.
   --------------------------------------------------------------------------------------------- */
SELECT
    OBJECT_NAME(fk.parent_object_id)                AS ReferencingTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ReferencingColumn,
    fk.delete_referential_action_desc               AS OnDelete
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.referenced_object_id = OBJECT_ID('dbo.Users')
ORDER BY OnDelete DESC, ReferencingTable;


/* ---------------------------------------------------------------------------------------------
   STEP 1 — The candidates, before any history is considered.

   Expect roughly 39. Eyeball the names: this is the list the window produced, and it is the last
   point at which a real person can be spotted by a human rather than by a rule.
   --------------------------------------------------------------------------------------------- */
SELECT
    u.Id,
    LTRIM(RTRIM(ISNULL(u.FirstName, '') + ' ' + ISNULL(u.LastName, ''))) AS Name,
    u.Email,
    u.CreatedOn,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.Payments            p WHERE p.UserId = u.Id)
           OR EXISTS (SELECT 1 FROM dbo.SessionRegistrations r WHERE r.UserId = u.Id)
           OR EXISTS (SELECT 1 FROM dbo.SessionAttendances   a WHERE a.UserId = u.Id)
           OR EXISTS (SELECT 1 FROM dbo.Waitlists            w WHERE w.UserId = u.Id)
           OR EXISTS (SELECT 1 FROM dbo.AccountCredits       c WHERE c.UserId = u.Id)
         THEN 'KEEP — has history' ELSE 'delete' END AS Verdict
FROM dbo.Users u
WHERE u.EmailConfirmed = 0
  AND u.IsAdmin        = 0
  AND u.CreatedOn      <  @Cutoff
  AND u.CreatedOn      >= @CreatedAfter
ORDER BY u.CreatedOn;


/* ---------------------------------------------------------------------------------------------
   STEP 2 — The rows that will actually go.

   Same predicate, minus anything the club would lose by removing it. A person who paid, booked,
   turned up, queued for a spot or holds a credit is kept whatever their confirmation state —
   that is what separates a real person whose confirmation mail went astray from litter.
   --------------------------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#Doomed') IS NOT NULL DROP TABLE #Doomed;

SELECT u.Id
INTO #Doomed
FROM dbo.Users u
WHERE u.EmailConfirmed = 0
  AND u.IsAdmin        = 0
  AND u.CreatedOn      <  @Cutoff
  AND u.CreatedOn      >= @CreatedAfter
  AND NOT EXISTS (SELECT 1 FROM dbo.Payments            p WHERE p.UserId = u.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.SessionRegistrations r WHERE r.UserId = u.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.SessionAttendances   a WHERE a.UserId = u.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.Waitlists            w WHERE w.UserId = u.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.AccountCredits       c WHERE c.UserId = u.Id);

SELECT COUNT(*) AS WillDelete FROM #Doomed;

-- Keep this. If anything goes wrong later, it is the only record of which rows were taken.
SELECT u.Id, u.FirstName, u.LastName, u.Email, u.CreatedOn
FROM dbo.Users u JOIN #Doomed d ON d.Id = u.Id
ORDER BY u.CreatedOn;


/* ---------------------------------------------------------------------------------------------
   STEP 3 — Delete.

   Runs inside a transaction and ROLLS BACK as written, so you can execute it once and see the
   counts without committing. When the numbers look right, change ROLLBACK to COMMIT on the last
   line and run it again.

   Notifications, SessionFeedbacks and WaiverAcceptances carry a UserId with no foreign key behind
   it, so nothing removes them on their own and they would sit there pointing at users that no
   longer exist. ReminderLogs does cascade; it is cleared anyway so the script does not depend on
   a constraint being what step 0 says it is. Everything else cascades from Users.
   --------------------------------------------------------------------------------------------- */
BEGIN TRANSACTION;

DELETE n FROM dbo.Notifications      n JOIN #Doomed d ON d.Id = n.UserId;
DELETE f FROM dbo.SessionFeedbacks   f JOIN #Doomed d ON d.Id = f.UserId;
DELETE w FROM dbo.WaiverAcceptances  w JOIN #Doomed d ON d.Id = w.UserId;
DELETE r FROM dbo.ReminderLogs       r JOIN #Doomed d ON d.Id = r.UserId;

DELETE u FROM dbo.Users u JOIN #Doomed d ON d.Id = u.Id;
SELECT @@ROWCOUNT AS UsersDeleted;

-- Change to COMMIT TRANSACTION when the counts above are what you expect.
ROLLBACK TRANSACTION;


/* ---------------------------------------------------------------------------------------------
   STEP 4 — Confirm, after committing.

   Both counts should be 0. The second is the one that matters: it is the same window, asked
   again.
   --------------------------------------------------------------------------------------------- */
-- SELECT COUNT(*) AS StillThere FROM dbo.Users u JOIN #Doomed d ON d.Id = u.Id;
-- SELECT COUNT(*) AS StillMatching FROM dbo.Users u
--  WHERE u.EmailConfirmed = 0 AND u.IsAdmin = 0
--    AND u.CreatedOn < DATEADD(day, -2, GETUTCDATE())
--    AND u.CreatedOn >= '2026-09-15T00:00:00';

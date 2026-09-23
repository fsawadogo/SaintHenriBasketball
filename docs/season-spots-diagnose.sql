/* =============================================================================================
   Why the countdown says "15 of 15 spots left".

   The popup subtracts the spot holders from the capacity. A holder is anyone in the union of
   three sets, and the popup shows 15 of 15 only when all three are empty for this season.
   Run this to see which of them is empty, and whether it ought to be.
   ============================================================================================= */

DECLARE @SeasonId uniqueidentifier =
    (SELECT TOP 1 Id FROM dbo.Seasons WHERE StartDate = '2026-09-26' ORDER BY StartDate);

SELECT @SeasonId AS SeasonId, Name, StartDate, Price, SeasonPassCapacity, Status
FROM dbo.Seasons WHERE Id = @SeasonId;
/* Price is what the popup shows verbatim. If $90 is wrong, it is wrong in this row —
   nothing computes it. Same field the plan-choice email quotes as the pass price. */


/* --- The three sets the popup unions ------------------------------------------------------- */

-- 1. Paid passes. NOTE: matched on Payments.SeasonId, so a season-pass payment recorded with a
--    NULL SeasonId is invisible here — this is the prime suspect.
SELECT COUNT(DISTINCT UserId) AS PaidPassHolders
FROM dbo.Payments
WHERE SeasonId = @SeasonId AND Plan = 0 AND Status = 1;   -- Plan 0 = Season, Status 1 = Completed

-- 2. Players who chose the pass in the plan prompt.
SELECT COUNT(DISTINCT UserId) AS ChoseThePass
FROM dbo.SeasonPlanChoices
WHERE SeasonId = @SeasonId AND Plan = 0;

-- 3. Players whose profile still carries the season plan.
SELECT COUNT(*) AS ProfileSaysSeason
FROM dbo.Users
WHERE PaymentPlan = 0 AND IsDeactivated = 0;


/* --- The suspect, stated plainly -----------------------------------------------------------
   Every completed season-pass payment, whatever season it points at. If rows come back here
   with a NULL or a different SeasonId, that is the whole bug: the money arrived, the popup
   cannot see it, and the season looks untouched. */
SELECT
    p.Id, p.UserId, u.FirstName, u.LastName, u.Email,
    p.Amount, p.SeasonId, p.SessionId, p.Status, p.CreatedAt, p.Reference
FROM dbo.Payments p
LEFT JOIN dbo.Users u ON u.Id = p.UserId
WHERE p.Plan = 0 AND p.Status = 1
ORDER BY p.CreatedAt DESC;


/* --- What the plan-choice email will say ---------------------------------------------------
   The email counts ONLY set 1, while the popup counts all three. The two disagree by however
   many players chose a pass without having paid yet — and that email goes to every member on
   Tuesday morning quoting this number. */
SELECT
    (SELECT SeasonPassCapacity FROM dbo.Seasons WHERE Id = @SeasonId)
      - (SELECT COUNT(DISTINCT UserId) FROM dbo.Payments
         WHERE SeasonId = @SeasonId AND Plan = 0 AND Status = 1)       AS EmailWillSay,
    (SELECT SeasonPassCapacity FROM dbo.Seasons WHERE Id = @SeasonId)
      - (SELECT COUNT(*) FROM (
            SELECT UserId FROM dbo.Payments WHERE SeasonId = @SeasonId AND Plan = 0 AND Status = 1
            UNION
            SELECT UserId FROM dbo.SeasonPlanChoices WHERE SeasonId = @SeasonId AND Plan = 0
            UNION
            SELECT Id FROM dbo.Users WHERE PaymentPlan = 0 AND IsDeactivated = 0
        ) AS Holders)                                                  AS PopupSays;

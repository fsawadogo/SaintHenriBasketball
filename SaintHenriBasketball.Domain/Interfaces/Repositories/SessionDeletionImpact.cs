namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Rows tied to a session that deleting it would remove (Payments block the delete instead).
public record SessionDeletionImpact(int Registrations, int AttendanceAnswers, int Waitlisted, int Feedback, int Recaps, int Payments);

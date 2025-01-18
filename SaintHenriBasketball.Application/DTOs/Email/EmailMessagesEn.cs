using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public static class EmailMessagesEn
{
    public static class Attendance
    {
        public static string ReminderMessage(string sessionDate, string sessionTime) =>
            $"This is a friendly reminder about your upcoming basketball session on Saturday, January 25, 2025 at 10 AM." +
            "Please arrive at least 15 minutes before the session starts to ensure a smooth start to practice." +
            @"<div style='margin-top: 20px; text-align: center;'>
            <p style='color: #444; margin: 15px 0;'>Please confirm your attendance by clicking the button below:</p>
            <a href='https://sainthenribasketball.com/attendance-confirmation' 
               style='background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
                Confirm Attendance
            </a>
        </div>";

        public static string LowAttendanceWarning =>
            "We've noticed that you've missed several recent sessions. " +
            "Regular attendance helps maintain team cohesion and ensures you get the most out of your membership. " +
            "Please let us know if there's anything we can do to help you attend more regularly.";

        public static string ConsecutiveMissedSessions =>
            "We noticed you've missed multiple consecutive sessions. " +
            "Your progress and participation are important to us. " +
            "If you're experiencing any difficulties attending, please let us know how we can help.";
    }

    public static class Season
    {
        public static string RegistrationReminder(string seasonStartDate, decimal price) =>
            $"The new basketball season starts on Saturday, January 25, 2025! " +
            $"Secure your spot by registering now." +
            "Early registration helps us better plan our sessions and ensure everyone gets the best experience possible.";

        public static string EarlyBirdRegistration(string deadline, decimal discountedPrice) =>
            $"Register before {deadline} to take advantage of our early bird rate of ${discountedPrice}. " +
            "Don't miss out on this special offer!";

        public static string LastCallRegistration(string deadline) =>
            $"Last call for season registration! The deadline is {deadline}. " +
            "Spots are filling up quickly, so don't wait to secure your place.";
    }

    public static class Payment
    {
        public static string PaymentDue(decimal amount) =>
            $"Your payment of ${amount} is due for the current season. " +
            "Please complete your payment to maintain your active status. " +
            "If you need to discuss payment arrangements, please don't hesitate to contact us.";

        public static string PaymentOverdue(decimal amount, int daysOverdue) =>
            $"Your payment of ${amount} is {daysOverdue} days overdue. " +
            "Please settle your payment as soon as possible to maintain your membership status. " +
            "If you're experiencing any difficulties, we're here to help - just let us know.";

        public static string PaymentPlanChange(PaymentPlan newPlan) =>
            $"Your payment plan has been updated to {GetPaymentPlanEnglish(newPlan)}. " +
            "This change will be effective from your next billing cycle. " +
            "Please ensure your payment information is up to date.";

        private static string GetPaymentPlanEnglish(PaymentPlan plan) => plan switch
        {
            PaymentPlan.Season => "Season",
            PaymentPlan.DropIn => "Drop-In",
            _ => plan.ToString()
        };
    }

    public static class General
    {
        public static string WelcomeMessage(string firstName) =>
            $"Welcome to Saint Henri Basketball, {firstName}! " +
            "We're excited to have you join our basketball community. " +
            "If you have any questions, our team is here to help.";

        public static string ScheduleChange =>
            "Important: There has been a change to our regular schedule. " +
            "Please check your upcoming sessions for updated times. " +
            "We apologize for any inconvenience this may cause.";

        public static string HolidaySchedule(string holidayPeriod) =>
            $"Please note our modified schedule during {holidayPeriod}. " +
            "Check your member portal for the detailed holiday schedule. " +
            "Regular sessions will resume after the holiday period.";

        public static string FacilityUpdate =>
            "We're continuously improving our facilities to enhance your basketball experience. " +
            "Please note there may be temporary adjustments to our usual setup. " +
            "We appreciate your understanding during this time.";
    }

    public static class Admin
    {
        public static string NewUserRegistration(string userName, PaymentPlan plan) =>
            $"New user registration: {userName}\n" +
            $"Payment Plan: {GetPaymentPlanEnglish(plan)}\n" +
            "Please review and ensure all registration requirements are met.";

        public static string LowAttendanceAlert(string userName, int missedSessions) =>
            $"Attendance Alert: {userName} has missed {missedSessions} sessions in the last month. " +
            "Consider reaching out to ensure everything is okay.";

        public static string PaymentStatusUpdate(string userName, decimal amount, string status) =>
            $"Payment Update for {userName}\n" +
            $"Amount: ${amount}\n" +
            $"Status: {status}";

        private static string GetPaymentPlanEnglish(PaymentPlan plan) => plan switch
        {
            PaymentPlan.Season => "Season",
            PaymentPlan.DropIn => "Drop-In",
            _ => plan.ToString()
        };
    }
}
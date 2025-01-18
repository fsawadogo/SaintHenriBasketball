using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public static class EmailMessagesFr
{
    public static class Attendance
    {
        public static string ReminderMessage(string sessionDate, string sessionTime) =>
            $"Ceci est un rappel amical concernant votre prochaine séance de basketball le samedi 25 janvier 2025. " +
            "Veuillez arriver au moins 15 minutes avant le début de la séance pour assurer un démarrage en douceur de l'entraînement." +
            @"<div style='margin-top: 20px; text-align: center;'>
            <p style='color: #444; margin: 15px 0;'>Veuillez confirmer votre présence en cliquant sur le bouton ci-dessous :</p>
            <a href='https://sainthenribasketball.com/attendance-confirmation' 
               style='background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
                Confirmer ma présence
            </a>
        </div>";

        public static string LowAttendanceWarning =>
            "Nous avons remarqué que vous avez manqué plusieurs séances récemment. " +
            "Une présence régulière aide à maintenir la cohésion de l'équipe et vous assure de tirer le meilleur parti de votre adhésion. " +
            "N'hésitez pas à nous contacter si nous pouvons vous aider à participer plus régulièrement.";

        public static string ConsecutiveMissedSessions =>
            "Nous avons remarqué que vous avez manqué plusieurs séances consécutives. " +
            "Vos progrès et votre participation sont importants pour nous. " +
            "Si vous rencontrez des difficultés pour assister aux séances, n'hésitez pas à nous en faire part.";
    }

    public static class Season
    {
        public static string RegistrationReminder(string seasonStartDate, decimal price) =>
            $"La nouvelle saison de basketball commence le {seasonStartDate}! " +
            $"Réservez votre place en vous inscrivant maintenant. Les frais de saison sont de {price}$. " +
            "Une inscription anticipée nous aide à mieux planifier nos séances et à assurer la meilleure expérience possible à tous.";

        public static string EarlyBirdRegistration(string deadline, decimal discountedPrice) =>
            $"Inscrivez-vous avant le {deadline} pour profiter de notre tarif préférentiel de {discountedPrice}$. " +
            "Ne manquez pas cette offre spéciale!";

        public static string LastCallRegistration(string deadline) =>
            $"Dernier appel pour les inscriptions de la saison! La date limite est le {deadline}. " +
            "Les places se remplissent rapidement, n'attendez pas pour réserver la vôtre.";
    }

    public static class Payment
    {
        public static string PaymentDue(decimal amount) =>
            $"Votre paiement de {amount}$ est dû pour la saison en cours. " +
            "Veuillez compléter votre paiement pour maintenir votre statut actif. " +
            "Si vous souhaitez discuter des modalités de paiement, n'hésitez pas à nous contacter.";

        public static string PaymentOverdue(decimal amount, int daysOverdue) =>
            $"Votre paiement de {amount}$ est en retard de {daysOverdue} jours. " +
            "Veuillez régler votre paiement dès que possible pour maintenir votre statut de membre. " +
            "Si vous rencontrez des difficultés, nous sommes là pour vous aider - faites-le nous savoir.";

        public static string PaymentPlanChange(PaymentPlan newPlan) =>
            $"Votre plan de paiement a été mis à jour vers {GetPaymentPlanFrench(newPlan)}. " +
            "Ce changement sera effectif à partir de votre prochain cycle de facturation. " +
            "Veuillez vous assurer que vos informations de paiement sont à jour.";

        private static string GetPaymentPlanFrench(PaymentPlan plan) => plan switch
        {
            PaymentPlan.Season => "Season",
            PaymentPlan.DropIn => "Par Séance",
            _ => plan.ToString()
        };
    }

    public static class General
    {
        public static string WelcomeMessage(string firstName) =>
            $"Bienvenue à Saint Henri Basketball, {firstName}! " +
            "Nous sommes ravis de vous accueillir dans notre communauté de basketball. " +
            "Si vous avez des questions, notre équipe est là pour vous aider.";

        public static string ScheduleChange =>
            "Important : Il y a eu un changement dans notre horaire régulier. " +
            "Veuillez vérifier vos prochaines séances pour les horaires mis à jour. " +
            "Nous nous excusons pour tout inconvénient que cela pourrait causer.";

        public static string HolidaySchedule(string holidayPeriod) =>
            $"Veuillez noter notre horaire modifié pendant {holidayPeriod}. " +
            "Consultez votre portail membre pour l'horaire détaillé des jours fériés. " +
            "Les séances régulières reprendront après la période des fêtes.";

        public static string FacilityUpdate =>
            "Nous améliorons continuellement nos installations pour enrichir votre expérience de basketball. " +
            "Veuillez noter qu'il pourrait y avoir des ajustements temporaires à notre configuration habituelle. " +
            "Nous vous remercions de votre compréhension pendant cette période.";
    }

    public static class Admin
    {
        public static string NewUserRegistration(string userName, PaymentPlan plan) =>
            $"Nouvelle inscription utilisateur : {userName}\n" +
            $"Plan de paiement : {GetPaymentPlanFrench(plan)}\n" +
            "Veuillez vérifier que toutes les exigences d'inscription sont respectées.";

        public static string LowAttendanceAlert(string userName, int missedSessions) =>
            $"Alerte de présence : {userName} a manqué {missedSessions} séances au cours du dernier mois. " +
            "Envisagez de prendre contact pour vous assurer que tout va bien.";

        public static string PaymentStatusUpdate(string userName, decimal amount, string status) =>
            $"Mise à jour du paiement pour {userName}\n" +
            $"Montant : {amount}$\n" +
            $"Statut : {status}";

        private static string GetPaymentPlanFrench(PaymentPlan plan) => plan switch
        {
            PaymentPlan.Season => "Saison",
            PaymentPlan.DropIn => "Par Séance",
            _ => plan.ToString()
        };
    }
}
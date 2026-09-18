using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Infrastructure.Data.Context;

/// The receipt PDF attached to payment confirmations.
///
/// The bytes cannot be read back as text, so these check the things that actually broke people:
/// it is produced at all, in both languages, with or without a logo on disk, and it grows when the
/// payment has a discount or credit to explain rather than swallowing them.
internal static class InvoicePdfChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var root = Path.Combine(Path.GetTempPath(), $"shb-invoice-{Guid.NewGuid():N}");
        var images = Path.Combine(root, "images");
        Directory.CreateDirectory(images);

        try
        {
            var plain = new BillDetails
            {
                Name = "Penielle Amouzou",
                Email = "penielle@example.test",
                Description = "Forfait de saison",
                Amount = 100m,
                Reference = "SEASON-4A7F",
                Date = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc),
                PaidOn = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc),
            };

            // --- With no logo file at all ---
            var noLogo = new BillPdfGenerator(new FakeEnvironment(root));
            var withoutLogo = noLogo.GenerateBill(plain);
            assert(IsPdf(withoutLogo),
                "invoice: a missing logo file still produces a receipt — it used to throw and the player got no attachment");

            // --- With the club mark present ---
            File.WriteAllBytes(Path.Combine(images, "club-mark.png"), OnePixelPng());
            var generator = new BillPdfGenerator(new FakeEnvironment(root));

            var french = generator.GenerateBill(plain);
            var english = generator.GenerateBill(plain, "en");
            assert(IsPdf(french) && IsPdf(english),
                "invoice: it renders in both languages");
            assert(french.Length != english.Length,
                "invoice: the two languages are genuinely different documents, not one language twice");

            // --- A payment reduced by a promo and by credit ---
            var reduced = new BillDetails
            {
                Name = "Penielle Amouzou",
                Email = "penielle@example.test",
                Description = "Forfait de saison",
                Amount = 92.15m,
                OriginalAmount = 100m,
                DiscountAmount = 5m,
                CreditApplied = 2.85m,
                Reference = "SEASON-4A7F",
                Date = plain.Date,
                PaidOn = plain.PaidOn,
            };
            var explained = generator.GenerateBill(reduced);
            // Set SHB_INVOICE_PROOF to a file path to keep a copy of the rendered receipt and look at
            // it — the bytes tell you it is a valid PDF, never whether the layout holds.
            var proofPath = Environment.GetEnvironmentVariable("SHB_INVOICE_PROOF");
            if (!string.IsNullOrWhiteSpace(proofPath)) File.WriteAllBytes(proofPath, generator.GenerateBill(reduced, "en"));
            assert(IsPdf(explained) && explained.Length > french.Length,
                "invoice: a discount and a credit each get their own line, instead of the receipt showing only the net");

            // --- Not yet paid ---
            var unpaid = new BillDetails
            {
                Name = "Penielle Amouzou",
                Description = "Forfait à la séance",
                Amount = 11m,
                Reference = "DROPIN-91C2",
                Date = plain.Date,
                PaidOn = null,
            };
            assert(IsPdf(generator.GenerateBill(unpaid)),
                "invoice: a bill for money not yet received still renders, without claiming it was paid");

            // --- Nothing to say about the charge ---
            var bare = new BillDetails { Name = "Penielle Amouzou", Amount = 0m, Date = plain.Date };
            assert(IsPdf(generator.GenerateBill(bare)),
                "invoice: a bill with no description and no reference does not crash on the nulls");

            return Task.CompletedTask;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* a temp directory is not worth failing over */ }
        }
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 1000 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F';

    /// A real 1x1 PNG, so XImage has something it can actually decode.
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string webRoot) => WebRootPath = webRoot;

        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "SaintHenriBasketball.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Development";
    }
}

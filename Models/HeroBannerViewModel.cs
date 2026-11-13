namespace TechStore.Models
{
    /// <summary>
    /// Modelo de vista para el banner superior reutilizable (_HeroBanner).
    /// </summary>
    public sealed class HeroBannerViewModel
    {
        public HeroBannerViewModel()
        {
        }

        public HeroBannerViewModel(
            string title,
            string subtitle,
            string? eyebrow = null,
            string? ctaText = null,
            string? ctaController = null,
            string? ctaAction = null)
        {
            Title = title;
            Subtitle = subtitle;
            Eyebrow = eyebrow;
            CtaText = ctaText;
            CtaController = ctaController;
            CtaAction = ctaAction;
        }

        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? Eyebrow { get; set; }
        public string? CtaText { get; set; }
        public string? CtaController { get; set; }
        public string? CtaAction { get; set; }
    }
}

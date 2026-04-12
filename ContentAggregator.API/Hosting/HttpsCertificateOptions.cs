namespace ContentAggregator.API.Hosting
{
    public sealed class HttpsCertificateOptions
    {
        public const string SectionName = "HttpsCertificate";

        public string Path { get; set; } = "/etc/ssl/certs/dev-cert.pfx";

        public string Password { get; set; } = string.Empty;
    }
}

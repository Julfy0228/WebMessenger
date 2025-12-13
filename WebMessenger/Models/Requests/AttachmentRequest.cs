using WebMessenger.Entities;

namespace WebMessenger.Models.Requests
{
    public class AttachmentRequest
    {
        public AttachmentType Type { get; set; }
        public string? Url { get; set; }
        public long? Size { get; set; }
        public string? Name { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Duration { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public int? TrackNumber { get; set; }
        public int? Bitrate { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}

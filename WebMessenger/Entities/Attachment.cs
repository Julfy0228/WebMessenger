using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public enum AttachmentType { File, Image, Audio, Video, Document, Link, Location }

    public abstract class Attachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [Required]
        public AttachmentType Type { get; protected set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Message? Message { get; set; }
    }

    public class FileAttachment : Attachment
    {
        public FileAttachment() =>
            Type = AttachmentType.File;

        [Required]
        [MaxLength(2048)]
        public string Url { get; set; } = string.Empty;

        [Required]
        public long? Size { get; set; }

        [Required]
        [MaxLength(255)]
        public string? Name { get; set; }

        public string? Extension => Name!.Split('.').LastOrDefault();
    }

    public class ImageAttachment : FileAttachment
    {
        public ImageAttachment() =>
            Type = AttachmentType.Image;

        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class AudioAttachment : FileAttachment
    {
        public AudioAttachment() =>
            Type = AttachmentType.Audio;

        public int Duration { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public int? TrackNumber { get; set; }
        public int? Bitrate { get; set; }
    }

    public class VideoAttachment : FileAttachment
    {
        public VideoAttachment() =>
            Type = AttachmentType.Video;

        public int Duration { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DocumentAttachment : FileAttachment
    {
        public DocumentAttachment() =>
            Type = AttachmentType.Document;
    }

    public class LinkAttachment : Attachment
    {
        public LinkAttachment() =>
            Type = AttachmentType.Link;

        [Required]
        [MaxLength(2048)]
        public string Url { get; set; } = string.Empty;
    }

    public class LocationAttachment : Attachment
    {
        public LocationAttachment() =>
            Type = AttachmentType.Location;

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }
    }
}

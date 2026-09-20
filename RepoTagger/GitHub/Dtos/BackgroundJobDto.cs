using static System.Net.Mime.MediaTypeNames;

namespace RepoTagger.GitHub.Dtos
{
    public class BackgroundJobDto
    {
        public long Id { get; set; }

        public string DeliveryId { get; set; }

        public string EventType { get; set; }

        public string Payload { get; set; }


        public string Status { get; set; }

        public int Attempts { get; set; } = 0;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public string LastError { get; set; }

        public DateTime? NextAttemptAtUtc { get; set; }
        public string Action { get; set; } = string.Empty;


    }
}

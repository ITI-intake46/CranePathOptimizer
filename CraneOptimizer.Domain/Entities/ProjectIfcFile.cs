using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CraneOptimizer.Domain.Entities
{
    public class ProjectIfcFile
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public Project Project { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        public bool Exists { get; set; }
    }
}
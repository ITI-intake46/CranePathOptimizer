using System.ComponentModel.DataAnnotations;

namespace CraneOptimizer.Domain.Entities
{
    public class Project
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime LastOpenedDate { get; set; }

        public DateTime LastModifiedDate { get; set; }

        public bool IsFavorite { get; set; }

        [MaxLength(4000)]
        public string Notes { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public int DisplayLengthUnit { get; set; } = 2;

        public int DisplayMassUnit { get; set; } = 1;

        public int DisplayAreaUnit { get; set; } = 0;

        public int UnitDecimalPlaces { get; set; } = 2;

        public ICollection<ProjectIfcFile> IfcFiles { get; set; } = new List<ProjectIfcFile>();
    }
}

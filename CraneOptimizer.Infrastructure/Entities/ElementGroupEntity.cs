using System;
using System.Collections.Generic;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// A named group of IFC elements (e.g. "Floor 3 Columns", "Roof Slabs").
    /// Useful for bulk lift planning — plan one lift for an entire group.
    /// </summary>
    public class ElementGroupEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   Name        { get; set; }
        public string   Description { get; set; }
        public string   ColorHex    { get; set; } = "#3498DB";
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity Project { get; set; }
        public ICollection<ElementGroupItemEntity> Items { get; set; } = new List<ElementGroupItemEntity>();
    }

    /// <summary>
    /// Junction table — many-to-many between ElementGroup and IfcElement.
    /// </summary>
    public class ElementGroupItemEntity
    {
        public int      Id          { get; set; }
        public int      GroupId     { get; set; }
        public int      ElementId   { get; set; }

        // Navigation
        public ElementGroupEntity  Group   { get; set; }
        public IfcElementEntity    Element { get; set; }
    }
}

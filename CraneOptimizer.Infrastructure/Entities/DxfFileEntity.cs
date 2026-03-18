using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Tracks DXF site drawings imported via DxfUploader.
    /// Stores the alignment transformation matrix so the DXF re-aligns
    /// automatically on project reload (replaces DxfFilePath + AlignmentMatrix
    /// that were saved in the JSON .craneproject file).
    /// </summary>
    public class DxfFileEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   FilePath    { get; set; }
        public string   FileName    { get; set; }
        public DateTime ImportedAt  { get; set; } = DateTime.UtcNow;
        public bool     IsActive    { get; set; } = true;

        // 4×4 alignment transformation matrix stored as 16 comma-separated doubles
        // e.g. "1,0,0,0,0,1,0,0,0,0,1,0,10.5,20.3,0,1"
        // null = no alignment applied yet
        public string   AlignmentMatrixJson { get; set; }

        // Alignment reference points (picked by user in DxfAlignmentDialog)
        public double?  RefPoint1_X { get; set; }
        public double?  RefPoint1_Y { get; set; }
        public double?  RefPoint2_X { get; set; }
        public double?  RefPoint2_Y { get; set; }
        public double?  Scale       { get; set; } = 1.0;
        public double?  RotationDeg { get; set; } = 0.0;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Services
{
    public class LiftPlanService
    {
        private readonly AppDbContext _db;
        public LiftPlanService(AppDbContext db) => _db = db;

        // ── Validate a proposed lift against the crane's load chart ─────────

        public record ValidationResult(
            bool IsValid,
            double CapacityTonne,
            double UtilisationPct,
            string Message,
            CraneLoadChartLookup.FeasibilityStatus Status);

        public async Task<ValidationResult> ValidateLiftAsync(
            int craneDbId, double radiusM, double boomLengthM, double weightKg)
        {
            double weightTonne = weightKg / 1000.0;

            // Load the crane entity to get its LoadChartId
            var crane = await _db.Cranes.FindAsync(craneDbId);
            if (crane == null)
                return new ValidationResult(false, 0, 0, "Crane not found.", CraneLoadChartLookup.FeasibilityStatus.NoChart);

            if (!crane.LoadChartId.HasValue)
                return new ValidationResult(false, 0, 0, "No load chart assigned to this crane.", CraneLoadChartLookup.FeasibilityStatus.NoChart);

            var points = await _db.CraneLoadChartPoints
                .Where(p => p.ChartId == crane.LoadChartId.Value && p.CapacityTonne.HasValue)
                .ToListAsync();

            if (points.Count == 0)
                return new ValidationResult(false, 0, 0, "Load chart has no data points.", CraneLoadChartLookup.FeasibilityStatus.NoChart);

            double? cap = CraneLoadChartLookup.GetCapacity(points, radiusM, boomLengthM);

            if (!cap.HasValue || cap.Value <= 0)
                return new ValidationResult(false, 0, 0,
                    $"No rated capacity at radius {radiusM:F1}m / boom {boomLengthM:F0}m.",
                    CraneLoadChartLookup.FeasibilityStatus.NoChart);

            double utilPct = weightTonne / cap.Value * 100.0;
            var status = CraneLoadChartLookup.Classify(utilPct);
            bool isValid = utilPct <= 95;

            string msg = status switch
            {
                CraneLoadChartLookup.FeasibilityStatus.Safe     => $"SAFE — {utilPct:F1}% utilisation",
                CraneLoadChartLookup.FeasibilityStatus.Marginal => $"MARGINAL — {utilPct:F1}% utilisation",
                _                                               => $"OVER-RATED — {utilPct:F1}% utilisation (max {cap.Value:F1} t)"
            };

            return new ValidationResult(isValid, cap.Value, utilPct, msg, status);
        }

        // ── Default checklist ──────────────────────────────────────────────

        public List<LiftChecklistItemEntity> GetDefaultChecklist()
        {
            var items = new[]
            {
                "Ground conditions checked",
                "Crane level verified",
                "Load weight confirmed",
                "Rigging inspected",
                "Tag lines ready",
                "Exclusion zone established",
                "Communication established with signal person",
                "Lift director briefed",
                "Weather conditions checked",
                "Emergency procedures reviewed"
            };
            return items.Select(i => new LiftChecklistItemEntity { Item = i }).ToList();
        }

        // ── CRUD ───────────────────────────────────────────────────────────

        public async Task<LiftPlanEntity> SaveAsync(LiftPlanEntity plan)
        {
            if (plan.Id == 0)
                _db.LiftPlans.Add(plan);
            else
                _db.LiftPlans.Update(plan);
            await _db.SaveChangesAsync();
            return plan;
        }

        public async Task DeleteAsync(int id)
        {
            var plan = await _db.LiftPlans.FindAsync(id);
            if (plan != null) { _db.LiftPlans.Remove(plan); await _db.SaveChangesAsync(); }
        }
    }
}

using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class CentralIfcPropertyAliasResolver : ICentralIfcPropertyAliasResolver
    {
        private readonly IIfcExporterProfileResolver _profileResolver;

        public CentralIfcPropertyAliasResolver(IIfcExporterProfileResolver profileResolver)
        {
            _profileResolver = profileResolver ?? throw new ArgumentNullException(nameof(profileResolver));
        }

        private static IIfcExporterProfile GetProfile(IfcExporterProfile profile)
        {
            return profile switch
            {
                IfcExporterProfile.Tekla => new TeklaIfcExporterProfile(),
                IfcExporterProfile.Revit => new RevitIfcExporterProfile(),
                _ => new GenericIfcExporterProfile()
            };
        }

        public CanonicalAssemblyIdentityResult ResolveCanonicalAssemblyIdentity(IIfcProduct? product, string previewName, IfcExporterProfile profileEnum)
        {
            var profile = GetProfile(profileEnum);
            var result = new CanonicalAssemblyIdentityResult
            {
                SourceSystem = profileEnum == IfcExporterProfile.Tekla ? SourceSystem.Tekla.ToString() :
                               profileEnum == IfcExporterProfile.Revit ? SourceSystem.Revit.ToString() :
                               SourceSystem.IFC.ToString(),
                Confidence = AssemblyMarkConfidence.None.ToString(),
                IsExplicitAssemblyMark = false,
                UsedFallback = false
            };

            if (product == null)
            {
                result.AssemblyMark = previewName;
                result.Confidence = AssemblyMarkConfidence.Low.ToString();
                result.UsedFallback = true;
                result.Warnings.Add("No active product instance; fell back to preview name.");
                return result;
            }

            // Phase 4B: build a property snapshot once per canonical identity resolution.
            // The canonical resolver remains the SSOT; this only avoids repeated IFC
            // property-set traversal for every alias candidate.
            var propertyLookup = BuildProductPropertyLookup(product);

            var psetNames = new[] { "Pset_Revit_Identity Data", "Pset_ConcreteElementGeneral" };

            // 1. Explicit assembly-level properties
            foreach (var targetPset in psetNames)
            {
                foreach (var propName in profile.AssemblyIdentityAliases)
                {
                    var val = GetPropertyString(propertyLookup, targetPset, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.AssemblyMark = val;
                        result.SourcePropertySet = targetPset;
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.High.ToString();
                        result.IsExplicitAssemblyMark = true;
                        return result;
                    }
                }
            }

            foreach (var propName in profile.AssemblyIdentityAliases)
            {
                var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.AssemblyMark = val;
                    result.SourcePropertySet = pset ?? "AnySet";
                    result.SourcePropertyName = propName;
                    result.Confidence = AssemblyMarkConfidence.High.ToString();
                    result.IsExplicitAssemblyMark = true;
                    return result;
                }
            }

            // 2. Exporter-specific assembly candidates (Revit assembly mark, Tekla cast unit)
            var isAssembly = product is IIfcElementAssembly || (product.ExpressType?.Name?.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase) ?? false);
            if (isAssembly)
            {
                foreach (var targetPset in psetNames)
                {
                    var val = GetPropertyString(propertyLookup, targetPset, "Mark");
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.AssemblyMark = val;
                        result.SourcePropertySet = targetPset;
                        result.SourcePropertyName = "Mark";
                        result.Confidence = AssemblyMarkConfidence.Medium.ToString();
                        result.IsExplicitAssemblyMark = true;
                        return result;
                    }
                }

                foreach (var propName in profile.AssemblyIdentityAliases)
                {
                    var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.AssemblyMark = val;
                        result.SourcePropertySet = pset ?? "AnySet";
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.Medium.ToString();
                        result.IsExplicitAssemblyMark = true;
                        return result;
                    }
                }
            }

            // 3. Explicit element-level mark
            foreach (var targetPset in psetNames)
            {
                foreach (var propName in profile.ElementIdentityAliases)
                {
                    // PartMark and PartPosition must not automatically become AssemblyMark
                    if (propName.Equals("Part Mark", StringComparison.OrdinalIgnoreCase) ||
                        propName.Equals("PartMark", StringComparison.OrdinalIgnoreCase) ||
                        propName.Equals("Part Position", StringComparison.OrdinalIgnoreCase) ||
                        propName.Equals("PartPosition", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var val = GetPropertyString(propertyLookup, targetPset, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.AssemblyMark = val;
                        result.SourcePropertySet = targetPset;
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.Medium.ToString();
                        result.IsExplicitAssemblyMark = false;
                        return result;
                    }
                }
            }

            foreach (var propName in profile.ElementIdentityAliases)
            {
                if (propName.Equals("Part Mark", StringComparison.OrdinalIgnoreCase) ||
                    propName.Equals("PartMark", StringComparison.OrdinalIgnoreCase) ||
                    propName.Equals("Part Position", StringComparison.OrdinalIgnoreCase) ||
                    propName.Equals("PartPosition", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.AssemblyMark = val;
                    result.SourcePropertySet = pset ?? "AnySet";
                    result.SourcePropertyName = propName;
                    result.Confidence = AssemblyMarkConfidence.Medium.ToString();
                    result.IsExplicitAssemblyMark = false;
                    return result;
                }
            }

            // 4. Type/reference candidates
            foreach (var targetPset in psetNames)
            {
                foreach (var propName in profile.TypeReferenceAliases)
                {
                    if (propName.Equals("ObjectType", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var val = GetPropertyString(propertyLookup, targetPset, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.AssemblyMark = val;
                        result.SourcePropertySet = targetPset;
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.Low.ToString();
                        result.IsExplicitAssemblyMark = false;
                        return result;
                    }
                }
            }

            foreach (var propName in profile.TypeReferenceAliases)
            {
                if (propName.Equals("ObjectType", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.AssemblyMark = val;
                    result.SourcePropertySet = pset ?? "AnySet";
                    result.SourcePropertyName = propName;
                    result.Confidence = AssemblyMarkConfidence.Low.ToString();
                    result.IsExplicitAssemblyMark = false;
                    return result;
                }
            }

            var objType = product.ObjectType?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(objType))
            {
                result.AssemblyMark = objType;
                result.SourcePropertySet = "ObjectType";
                result.SourcePropertyName = "ObjectType";
                result.Confidence = AssemblyMarkConfidence.Low.ToString();
                result.IsExplicitAssemblyMark = false;
                return result;
            }

            // 5. Display fallback only: product.Name
            var prodName = product.Name?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(prodName))
            {
                result.AssemblyMark = prodName;
                result.SourcePropertySet = "Product";
                result.SourcePropertyName = "Name";
                result.Confidence = AssemblyMarkConfidence.Low.ToString();
                result.IsExplicitAssemblyMark = false;
                result.UsedFallback = true;
                result.Warnings.Add("Fell back to product Name; no explicit assembly or element mark was found.");
            }
            else
            {
                result.AssemblyMark = previewName;
                result.SourcePropertySet = "Preview";
                result.SourcePropertyName = "Name";
                result.Confidence = AssemblyMarkConfidence.None.ToString();
                result.IsExplicitAssemblyMark = false;
                result.UsedFallback = true;
                result.Warnings.Add("No name or mark properties found; fell back to preview name.");
            }

            return result;
        }

        public SourcePropertyResolutionResult ResolveProperty(IIfcProduct product, string logicalField, IfcExporterProfile profileEnum)
        {
            var profile = GetProfile(profileEnum);
            var result = new SourcePropertyResolutionResult
            {
                LogicalField = logicalField,
                SourceSystem = profileEnum == IfcExporterProfile.Tekla ? SourceSystem.Tekla.ToString() :
                               profileEnum == IfcExporterProfile.Revit ? SourceSystem.Revit.ToString() :
                               SourceSystem.IFC.ToString(),
                ExporterProfile = profileEnum.ToString(),
                Confidence = AssemblyMarkConfidence.None.ToString()
            };

            // Phase 4B: one cheap snapshot for string alias lookups in this call.
            var propertyLookup = BuildProductPropertyLookup(product);

            if (logicalField.Equals("Weight", StringComparison.OrdinalIgnoreCase) ||
                logicalField.Equals("AssemblyWeight", StringComparison.OrdinalIgnoreCase) ||
                logicalField.Equals("ElementWeight", StringComparison.OrdinalIgnoreCase))
            {
                // Weight lookup: 
                // 1. Quantities
                var quantitySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                    .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

                foreach (var qSet in quantitySets)
                {
                    foreach (var quantity in qSet.Quantities.OfType<IIfcQuantityWeight>())
                    {
                        var qname = quantity.Name.ToString();
                        if (ContainsAny(qname, profile.WeightAliases) && IsTotalWeightProperty(qname))
                        {
                            result.Value = ((double)quantity.WeightValue).ToString(CultureInfo.InvariantCulture);
                            result.SourcePropertySet = qSet.Name.ToString();
                            result.SourcePropertyName = qname;
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }
                }

                // 2. Property sets
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pSet in propertySets)
                {
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var name = prop.Name.ToString();
                        if (ContainsAny(name, profile.WeightAliases) && IsTotalWeightProperty(name))
                        {
                            if (TryReadNumericPropertyValue(prop, out var parsedVal))
                            {
                                result.Value = parsedVal.ToString(CultureInfo.InvariantCulture);
                                result.SourcePropertySet = pSet.Name.ToString();
                                result.SourcePropertyName = name;
                                result.Confidence = AssemblyMarkConfidence.High.ToString();
                                return result;
                            }
                        }
                    }
                }
            }
            else if (logicalField.Equals("Volume", StringComparison.OrdinalIgnoreCase))
            {
                // Volume lookup:
                // 1. Quantities
                var quantitySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                    .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

                foreach (var qSet in quantitySets)
                {
                    foreach (var quantity in qSet.Quantities.OfType<IIfcQuantityVolume>())
                    {
                        if (ContainsAny(quantity.Name.ToString(), profile.VolumeAliases))
                        {
                            result.Value = ((double)quantity.VolumeValue).ToString(CultureInfo.InvariantCulture);
                            result.SourcePropertySet = qSet.Name.ToString();
                            result.SourcePropertyName = quantity.Name.ToString();
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }
                }

                // 2. Property sets
                var propertySets2 = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pSet in propertySets2)
                {
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var name = prop.Name.ToString();
                        if (ContainsAny(name, profile.VolumeAliases))
                        {
                            if (TryReadNumericPropertyValue(prop, out var parsedVal))
                            {
                                result.Value = parsedVal.ToString(CultureInfo.InvariantCulture);
                                result.SourcePropertySet = pSet.Name.ToString();
                                result.SourcePropertyName = name;
                                result.Confidence = AssemblyMarkConfidence.High.ToString();
                                return result;
                            }
                        }
                    }
                }
            }
            else if (logicalField.Equals("Material", StringComparison.OrdinalIgnoreCase))
            {
                // Material lookup:
                var relation = product.HasAssociations?
                    .OfType<IIfcRelAssociatesMaterial>()
                    .FirstOrDefault();

                if (relation != null)
                {
                    var resolvedName = ResolveMaterialName(relation.RelatingMaterial);
                    if (!string.IsNullOrWhiteSpace(resolvedName))
                    {
                        result.Value = resolvedName;
                        result.SourcePropertySet = "Associations";
                        result.SourcePropertyName = "RelatingMaterial";
                        result.Confidence = AssemblyMarkConfidence.High.ToString();
                        return result;
                    }
                }

                // Type Associations
                foreach (var rel in product.IsDefinedBy.OfType<IIfcRelDefinesByType>())
                {
                    var typeObject = rel.RelatingType;
                    var typeMat = typeObject.HasAssociations?.OfType<IIfcRelAssociatesMaterial>().FirstOrDefault();
                    if (typeMat != null)
                    {
                        var resolvedName = ResolveMaterialName(typeMat.RelatingMaterial);
                        if (!string.IsNullOrWhiteSpace(resolvedName))
                        {
                            result.Value = resolvedName;
                            result.SourcePropertySet = "TypeAssociations";
                            result.SourcePropertyName = "RelatingMaterial";
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }

                    foreach (var pset in typeObject.HasPropertySets.OfType<IIfcPropertySet>())
                    {
                        foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                        {
                            var propName = prop.Name.ToString();
                            if (ContainsAny(propName, profile.MaterialAliases))
                            {
                                var val = prop.NominalValue?.ToString();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    result.Value = val;
                                    result.SourcePropertySet = pset.Name.ToString();
                                    result.SourcePropertyName = propName;
                                    result.Confidence = AssemblyMarkConfidence.High.ToString();
                                    return result;
                                }
                            }
                        }
                    }
                }

                // Property sets
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pSet in propertySets)
                {
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var name = prop.Name.ToString();
                        if (ContainsAny(name, profile.MaterialAliases))
                        {
                            var val = prop.NominalValue?.ToString();
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                result.Value = val;
                                result.SourcePropertySet = pSet.Name.ToString();
                                result.SourcePropertyName = name;
                                result.Confidence = AssemblyMarkConfidence.High.ToString();
                                return result;
                            }
                        }
                    }
                }
            }
            else if (logicalField.Equals("Level", StringComparison.OrdinalIgnoreCase) ||
                     logicalField.Equals("TopLevel", StringComparison.OrdinalIgnoreCase) ||
                     logicalField.Equals("BottomLevel", StringComparison.OrdinalIgnoreCase) ||
                     logicalField.Equals("Storey", StringComparison.OrdinalIgnoreCase))
            {
                // Level/Storey lookup
                var psetNames = new[] { "Pset_Revit_Identity Data", "Pset_ConcreteElementGeneral" };
                foreach (var targetPset in psetNames)
                {
                    foreach (var propName in profile.LevelAliases)
                    {
                        var val = GetPropertyString(propertyLookup, targetPset, propName);
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            result.Value = val;
                            result.SourcePropertySet = targetPset;
                            result.SourcePropertyName = propName;
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }
                }

                foreach (var propName in profile.LevelAliases)
                {
                    var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.Value = val;
                        result.SourcePropertySet = pset ?? "AnySet";
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.High.ToString();
                        return result;
                    }
                }
            }
            else if (logicalField.Equals("ElementMark", StringComparison.OrdinalIgnoreCase) ||
                     logicalField.Equals("PartMark", StringComparison.OrdinalIgnoreCase))
            {
                // Element/Part Mark lookup
                var psetNames = new[] { "Pset_Revit_Identity Data", "Pset_ConcreteElementGeneral" };
                foreach (var targetPset in psetNames)
                {
                    foreach (var propName in profile.ElementIdentityAliases)
                    {
                        var val = GetPropertyString(propertyLookup, targetPset, propName);
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            result.Value = val;
                            result.SourcePropertySet = targetPset;
                            result.SourcePropertyName = propName;
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }
                }

                foreach (var propName in profile.ElementIdentityAliases)
                {
                    var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.Value = val;
                        result.SourcePropertySet = pset ?? "AnySet";
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.High.ToString();
                        return result;
                    }
                }
            }
            else if (logicalField.Equals("TypeMark", StringComparison.OrdinalIgnoreCase) ||
                     logicalField.Equals("Reference", StringComparison.OrdinalIgnoreCase))
            {
                // Type/Reference lookup
                var psetNames = new[] { "Pset_Revit_Identity Data", "Pset_ConcreteElementGeneral" };
                foreach (var targetPset in psetNames)
                {
                    foreach (var propName in profile.TypeReferenceAliases)
                    {
                        var val = GetPropertyString(propertyLookup, targetPset, propName);
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            result.Value = val;
                            result.SourcePropertySet = targetPset;
                            result.SourcePropertyName = propName;
                            result.Confidence = AssemblyMarkConfidence.High.ToString();
                            return result;
                        }
                    }
                }

                foreach (var propName in profile.TypeReferenceAliases)
                {
                    var (val, pset) = GetPropertyStringAnySet(propertyLookup, propName);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result.Value = val;
                        result.SourcePropertySet = pset ?? "AnySet";
                        result.SourcePropertyName = propName;
                        result.Confidence = AssemblyMarkConfidence.High.ToString();
                        return result;
                    }
                }
            }

            return result;
        }

        private static bool ContainsAny(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var kw in keywords)
            {
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private sealed record ProductPropertyValue(string Value, string PropertySetName);

        private static Dictionary<string, List<ProductPropertyValue>> BuildProductPropertyLookup(IIfcProduct product)
        {
            var lookup = new Dictionary<string, List<ProductPropertyValue>>(StringComparer.OrdinalIgnoreCase);

            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pset in propertySets)
            {
                var psetName = pset.Name.ToString();

                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    var propName = prop.Name.ToString();
                    var val = prop.NominalValue?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(propName) || string.IsNullOrWhiteSpace(val))
                        continue;

                    if (!lookup.TryGetValue(propName, out var values))
                    {
                        values = new List<ProductPropertyValue>();
                        lookup[propName] = values;
                    }

                    values.Add(new ProductPropertyValue(val, psetName));
                }
            }

            return lookup;
        }

        private static string? GetPropertyString(
            IReadOnlyDictionary<string, List<ProductPropertyValue>> propertyLookup,
            string psetName,
            string propName)
        {
            if (!propertyLookup.TryGetValue(propName, out var values))
                return null;

            foreach (var value in values)
            {
                if (value.PropertySetName.Contains(psetName, StringComparison.OrdinalIgnoreCase))
                    return value.Value;
            }

            return null;
        }

        private static (string? val, string? psetName) GetPropertyStringAnySet(
            IReadOnlyDictionary<string, List<ProductPropertyValue>> propertyLookup,
            string propName)
        {
            if (!propertyLookup.TryGetValue(propName, out var values) || values.Count == 0)
                return (null, null);

            var value = values[0];
            return (value.Value, value.PropertySetName);
        }

        private static string? GetPropertyString(IIfcProduct product, string psetName, string propName)
        {
            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pset in propertySets)
            {
                if (!pset.Name.ToString().Contains(psetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    if (!prop.Name.ToString().Equals(propName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var val = prop.NominalValue?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }

            return null;
        }

        private static (string? val, string? psetName) GetPropertyStringAnySet(IIfcProduct product, string propName)
        {
            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pset in propertySets)
            {
                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    if (!prop.Name.ToString().Equals(propName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var val = prop.NominalValue?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        return (val, pset.Name.ToString());
                }
            }

            return (null, null);
        }

        private static string? ResolveMaterialName(object? materialObject)
        {
            return materialObject switch
            {
                IIfcMaterial singleMaterial =>
                    singleMaterial.Name.ToString(),

                IIfcMaterialLayer layer =>
                    layer.Material?.Name.ToString(),

                IIfcMaterialLayerSet layerSet =>
                    JoinDistinctMaterials(layerSet.MaterialLayers.Select(l => l.Material?.Name.ToString())),

                IIfcMaterialLayerSetUsage layerSetUsage =>
                    JoinDistinctMaterials(layerSetUsage.ForLayerSet.MaterialLayers.Select(l => l.Material?.Name.ToString())),

                IIfcMaterialList materialList =>
                    JoinDistinctMaterials(materialList.Materials.Select(m => m.Name.ToString())),

                _ => null
            };
        }

        private static string? JoinDistinctMaterials(IEnumerable<string?> materials)
        {
            var distinct = materials
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return distinct.Count > 0 ? string.Join(", ", distinct) : null;
        }

        private static bool TryReadNumericPropertyValue(IIfcPropertySingleValue property, out double value)
        {
            value = 0;

            var nominal = property.NominalValue;
            if (nominal == null)
                return false;

            object? rawValue = nominal;
            var valueProperty = nominal.GetType().GetProperty("Value");
            if (valueProperty != null)
            {
                rawValue = valueProperty.GetValue(nominal);
            }

            if (rawValue is double doubleValue)
            {
                value = doubleValue;
                return true;
            }

            if (rawValue is float floatValue)
            {
                value = floatValue;
                return true;
            }

            if (rawValue is decimal decimalValue)
            {
                value = (double)decimalValue;
                return true;
            }

            if (rawValue is int intValue)
            {
                value = intValue;
                return true;
            }

            if (rawValue is long longValue)
            {
                value = longValue;
                return true;
            }

            var valueText = Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            if (double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            valueText = Convert.ToString(nominal, CultureInfo.InvariantCulture);
            if (double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            var numericMatch = System.Text.RegularExpressions.Regex.Match(valueText ?? string.Empty, @"[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?");
            return numericMatch.Success &&
                   double.TryParse(numericMatch.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static readonly string[] ExcludedWeightKeywords = new[]
        {
            "rebar", "reinforcement", "mesh", "steel", "paint", "grout", "strand", "weld", "bolt", "connection", "markup"
        };

        private static bool IsTotalWeightProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return false;
            foreach (var kw in ExcludedWeightKeywords)
            {
                if (propertyName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}

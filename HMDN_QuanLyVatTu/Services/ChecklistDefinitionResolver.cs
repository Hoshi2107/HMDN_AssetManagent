using System;
using System.Collections.Generic;
using System.Linq;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Services
{
    public class ChecklistDefinitionResolver
    {
        public IEnumerable<ChecklistDefinition> ResolveApplicableDefinitions(IEnumerable<ChecklistDefinition> rawDefinitions)
        {
            if (rawDefinitions == null) return Enumerable.Empty<ChecklistDefinition>();

            var resolved = new List<ChecklistDefinition>();

            // Nhóm theo khóa định danh nghiệp vụ: DefinitionCode hoặc cặp (CheckName, CycleType) làm fallback
            var grouped = rawDefinitions.GroupBy(d => {
                string codeKey = !string.IsNullOrWhiteSpace(d.DefinitionCode)
                    ? d.DefinitionCode.Trim().ToLower()
                    : null;

                string nameKey = !string.IsNullOrWhiteSpace(d.CheckName)
                    ? d.CheckName.Trim().ToLower()
                    : "";

                string cycleKey = !string.IsNullOrWhiteSpace(d.CycleType)
                    ? d.CycleType.Trim().ToLower()
                    : "";

                return new { CodeKey = codeKey, NameKey = nameKey, CycleKey = cycleKey };
            });

            foreach (var group in grouped)
            {
                // Nếu có CodeKey (nhóm theo mã), ta ghi đè chính xác dựa trên độ ưu tiên của scope
                if (group.Key.CodeKey != null)
                {
                    var bestDefinition = group.OrderByDescending(d => GetScopePriority(d.Scope)).FirstOrDefault();
                    if (bestDefinition != null)
                    {
                        resolved.Add(bestDefinition);
                    }
                }
                else
                {
                    // Nếu không có CodeKey (nhóm theo tên/chu kỳ), ta fallback ghi đè dựa trên tên và chu kỳ
                    var bestDefinition = group.OrderByDescending(d => GetScopePriority(d.Scope)).FirstOrDefault();
                    if (bestDefinition != null)
                    {
                        resolved.Add(bestDefinition);
                    }
                }
            }

            return resolved.OrderBy(d => d.SortOrder).ToList();
        }

        private ChecklistScope GetScopePriority(string scopeStr)
        {
            if (string.IsNullOrWhiteSpace(scopeStr)) return ChecklistScope.Global;

            switch (scopeStr.ToLower())
            {
                case "inventory": return ChecklistScope.Inventory;
                case "location": return ChecklistScope.Location;
                case "item": return ChecklistScope.Item;
                case "group": return ChecklistScope.Group;
                default: return ChecklistScope.Global;
            }
        }
    }
}

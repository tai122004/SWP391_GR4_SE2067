using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RWPM.Common
{
    public class UIHelper
    {
        public static List<SelectListItem> GenerateSelectListFromEnum<TEnum>(string emptySelectValue = "--- Choose ---", TEnum? selectedValue = null) where TEnum : struct, Enum
        {
            var data = new List<SelectListItem>();
            data.Add(new SelectListItem()
            {
                Value = string.Empty,
                Text = emptySelectValue,
                Selected = true
            });

            data.AddRange(Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new SelectListItem
                {
                    Value = Convert.ToInt32(e).ToString(),
                    Text = GetDisplayName(e),
                    Selected = selectedValue != null && e.Equals(selectedValue)
                })
                .ToList());

            return data;
        }

        public static string GetDisplayName(Enum enumValue)
        {
            var memberInfo = enumValue.GetType().GetMember(enumValue.ToString()).FirstOrDefault();
            if (memberInfo == null) return enumValue.ToString();

            var displayAttr = memberInfo.GetCustomAttribute<DisplayAttribute>();
            return displayAttr?.GetName() ?? enumValue.ToString();
        }
    }
}

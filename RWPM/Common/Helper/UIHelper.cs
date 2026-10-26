using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RWPM.Common
{
    public class UIHelper
    {
        /// <param name="emptySelectValue">Text of the empty option; null = SharedResource.Dropdown_Choose (current UI language)</param>
        public static List<SelectListItem> GenerateSelectListFromEnum<TEnum>(string? emptySelectValue = null, TEnum? selectedValue = null) where TEnum : struct, Enum
        {
            var data = new List<SelectListItem>();
            data.Add(new SelectListItem()
            {
                Value = string.Empty,
                Text = emptySelectValue ?? SharedResource.Dropdown_Choose,
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

        /// <summary>
        /// Tên hiển thị của enum theo ngôn ngữ hiện tại (CultureInfo.CurrentUICulture):
        /// 1. [Display(ResourceType = ..., Name = "Key")] -> đọc từ resx tương ứng
        /// 2. Không có Display -> tìm key quy ước "Enum_{TênEnum}_{GiáTrị}" trong SharedResource
        /// 3. Không có -> tên giá trị enum (giá trị không định nghĩa, VD role cũ = 3 -> "3")
        /// </summary>
        public static string GetDisplayName(Enum enumValue)
        {
            var name = enumValue.ToString();
            var memberInfo = enumValue.GetType().GetMember(name).FirstOrDefault();
            if (memberInfo == null) return name;

            var displayAttr = memberInfo.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null)
            {
                try
                {
                    var display = displayAttr.GetName();
                    if (!string.IsNullOrEmpty(display)) return display;
                }
                catch (InvalidOperationException)
                {
                    // ResourceType không có thuộc tính Name tương ứng -> dùng các bước dự phòng bên dưới
                }
            }

            var byConvention = SharedResource.ResourceManager.GetString($"Enum_{enumValue.GetType().Name}_{name}", SharedResource.Culture);
            return string.IsNullOrEmpty(byConvention) ? name : byConvention;
        }
    }
}

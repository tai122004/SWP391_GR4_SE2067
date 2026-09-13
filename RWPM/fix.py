import re

path = r"f:\SWP\SWP391_GR4_SE2067\RWPM\Views\ShiftRegistration\Index.cshtml"
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('@using Microsoft.AspNetCore.Mvc.Localization\n@inject Microsoft.Extensions.Localization.IStringLocalizer<RWPM.Resources.Shared.ShiftRegistrationResource> Localizer\n\n', '@using RWPM.Resources.Shared\n\n')
content = content.replace('@using Microsoft.AspNetCore.Mvc.Localization\r\n@inject Microsoft.Extensions.Localization.IStringLocalizer<RWPM.Resources.Shared.ShiftRegistrationResource> Localizer\r\n\r\n', '@using RWPM.Resources.Shared\r\n\r\n')
content = re.sub(r'@Localizer\["(.*?)"\]', r'@ShiftRegistrationResource.\1', content)
content = re.sub(r'@Html.Raw\(Localizer\["(.*?)"\].Value\)', r'@Html.Raw(ShiftRegistrationResource.\1)', content)
content = content.replace("locale: 'vi',", "locale: '@(System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName)',")

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Done")

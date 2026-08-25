using System;
using System.Collections.Generic;
using System.Text;

namespace EFTBallisticCalculator
{
    internal sealed class ConfigurationManagerAttributes
    {
        // 用于覆盖 F12 菜单中显示的配置项名称 (Key)
        public string DispName;

        // 甚至可以用来排序，数字越小越靠上
        public int? Order;

        // 如果设置为 true，这个设置项在高级设置里才显示
        public bool? IsAdvanced;
    }
}

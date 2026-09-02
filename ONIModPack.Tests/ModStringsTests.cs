using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Localization;

namespace ONIModPack.Tests
{
    /// <summary>
    /// 多语言收口回归：ModStrings 回落链（外部表 → 内置默认 zh/en → key 本身）
    /// 与 strings/strings.po 的词表一致性（代码 Register 与 po msgid 一一对应）。
    /// </summary>
    public class ModStringsTests
    {
        [Fact]
        public void S_ExternalLookupTakesPriority()
        {
            ModStrings.ExternalLookup = _ => null;
            var key = ModStrings.Prefix + "Screen.Social.Close";
            var fromTable = ModStrings.S(key);

            ModStrings.ExternalLookup = k => k == key ? "TABLE_VALUE" : null;
            Assert.Equal("TABLE_VALUE", ModStrings.S(key));
            ModStrings.ExternalLookup = null;
            Assert.NotEqual("TABLE_VALUE", fromTable); // 断开外部表后回到内置默认
        }

        [Fact]
        public void S_MissingKeyFallsBackToKeyItself()
        {
            ModStrings.ExternalLookup = null;
            Assert.Equal("ONIModPack.NoSuch.Key", ModStrings.S("ONIModPack.NoSuch.Key"));
        }

        [Theory]
        [InlineData("Screen.Social.Close", "关闭")]
        [InlineData("Vitals.Faith", "信仰")]
        [InlineData("Notify.Strike.Detail", "罢工爆发！{0} 名工人罢工（原因：{1}）")]
        public void Defaults_ContainChineseFallbacks(string suffix, string expectedZh)
        {
            Assert.True(ModStringDefaults.TryGet(ModStrings.Prefix + suffix, true, out var zh), $"missing key: {suffix}");
            Assert.Equal(expectedZh, zh);
        }

        [Theory]
        [InlineData("Screen.Social.Close", "Close")]
        [InlineData("Vitals.Faith", "Faith")]
        [InlineData("Overlay.Faith", "Faith & Stress")]
        public void Defaults_ContainEnglishFallbacks(string suffix, string expectedEn)
        {
            Assert.True(ModStringDefaults.TryGet(ModStrings.Prefix + suffix, false, out var en), $"missing key: {suffix}");
            Assert.Equal(expectedEn, en);
        }

        [Fact]
        public void F_FormatsPlaceholders()
        {
            ModStrings.ExternalLookup = null;
            var result = ModStrings.F(ModStrings.Prefix + "Screen.Social.Colony.Stress", 55.4f);
            Assert.Contains("55", result); // {0:F0} 四舍五入
        }

        [Fact]
        public void PoFile_CoversAllRegisteredKeys()
        {
            // 解析 strings/strings.po 的 msgid，与 ModStringDefaults.AllKeys 对齐
            var repoRoot = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),
                "..", "..", "..", "..");
            var poPath = System.IO.Path.Combine(repoRoot, "strings", "strings.po");
            if (!System.IO.File.Exists(poPath)) return; // 打包环境无仓库时跳过

            var poKeys = new HashSet<string>();
            foreach (var line in System.IO.File.ReadAllLines(poPath))
            {
                if (line.StartsWith("msgid \"") && !line.StartsWith("msgid \"\""))
                    poKeys.Add(line.Substring("msgid \"".Length).TrimEnd('"'));
            }

            var missing = new List<string>();
            foreach (var key in ModStringDefaults.AllKeys)
                if (!poKeys.Contains(key)) missing.Add(key);

            Assert.Empty(missing);
        }
    }
}

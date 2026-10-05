using ShutdownTimer.Helpers;
using Xunit;

namespace ShutdownTimer.Tests
{
    /// <summary>
    /// 兼容性契约测试。
    ///
    /// 汉化最容易搞坏的不是界面，而是"显示文本被当业务标识用"的地方：
    /// settings.json 里存的 Action 字符串、CLI 的 /SetAction 参数、
    /// 以及优雅模式的可用性判断。这些测试锁住上游原有行为，
    /// 任何改动只要让老配置或老脚本失效，这里就会红。
    /// </summary>
    public class PowerActionTests
    {
        // ---------- 上游 settings.json / CLI 里出现过的值必须继续可用 ----------

        [Theory]
        [InlineData("Shutdown", PowerAction.Shutdown)]
        [InlineData("Restart", PowerAction.Restart)]
        [InlineData("Hibernate", PowerAction.Hibernate)]
        [InlineData("Sleep", PowerAction.Sleep)]
        [InlineData("Logout", PowerAction.Logout)]
        [InlineData("Lock", PowerAction.Lock)]
        [InlineData("Custom Command", PowerAction.CustomCommand)]
        public void Upstream_values_still_parse(string raw, PowerAction expected)
        {
            Assert.True(PowerActions.TryParse(raw, out var actual), $"'{raw}' 应当可解析");
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("shutdown")]
        [InlineData("SHUTDOWN")]
        [InlineData("Shutdown ")]
        [InlineData(" custom command ")]
        public void Parsing_is_case_insensitive_and_trims(string raw)
        {
            Assert.True(PowerActions.TryParse(raw, out _), $"'{raw}' 应当可解析");
        }

        [Theory]
        [InlineData("Reboot", PowerAction.Restart)]
        [InlineData("Logoff", PowerAction.Logout)]
        [InlineData("CustomCommand", PowerAction.CustomCommand)]
        public void Legacy_aliases_still_work(string raw, PowerAction expected)
        {
            Assert.True(PowerActions.TryParse(raw, out var actual));
            Assert.Equal(expected, actual);
        }

        // ---------- 写进用户配置文件的拼写不能变 ----------

        [Fact]
        public void Keys_match_upstream_spelling()
        {
            // Key() 的返回值会写进 settings.json。改了就等于弄坏老用户的配置。
            Assert.Equal("Shutdown", PowerAction.Shutdown.Key());
            Assert.Equal("Restart", PowerAction.Restart.Key());
            Assert.Equal("Hibernate", PowerAction.Hibernate.Key());
            Assert.Equal("Sleep", PowerAction.Sleep.Key());
            Assert.Equal("Logout", PowerAction.Logout.Key());
            Assert.Equal("Lock", PowerAction.Lock.Key());
            Assert.Equal("Custom Command", PowerAction.CustomCommand.Key());
        }

        [Fact]
        public void Round_trip_through_key_is_stable()
        {
            foreach (var action in PowerActions.All)
            {
                Assert.True(PowerActions.TryParse(action.Key(), out var parsed));
                Assert.Equal(action, parsed);
            }
        }

        // ---------- 优雅模式的可用性判断必须与上游一致 ----------

        [Fact]
        public void Graceful_gate_matches_upstream_rule()
        {
            // 上游原写法：actionComboBox.Text == "Shutdown" || "Restart" || "Logout"
            Assert.True(PowerAction.Shutdown.SupportsGraceful());
            Assert.True(PowerAction.Restart.SupportsGraceful());
            Assert.True(PowerAction.Logout.SupportsGraceful());

            Assert.False(PowerAction.Hibernate.SupportsGraceful());
            Assert.False(PowerAction.Sleep.SupportsGraceful());
            Assert.False(PowerAction.Lock.SupportsGraceful());
            Assert.False(PowerAction.CustomCommand.SupportsGraceful());
        }

        // ---------- 非法输入不能被静默吞掉 ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("随便写的")]
        [InlineData("TurnOff")]
        public void Unknown_values_are_rejected(string raw)
        {
            Assert.False(PowerActions.TryParse(raw, out _), $"'{raw}' 不应被接受");
        }

        [Fact]
        public void ParseOrDefault_falls_back_for_cli_paths()
        {
            Assert.Equal(PowerAction.Shutdown, PowerActions.ParseOrDefault("不存在的值"));
            Assert.Equal(PowerAction.Sleep, PowerActions.ParseOrDefault("不存在的值", PowerAction.Sleep));
        }

        // ---------- 中文显示名不能污染内部标识 ----------

        [Fact]
        public void Chinese_display_names_are_accepted_but_never_stored()
        {
            Loc.ApplyLanguage(Loc.LangChinese);

            // CLI 里传中文名也要认（用户照着界面抄参数）
            Assert.True(PowerActions.TryParse("关机", out var a));
            Assert.Equal(PowerAction.Shutdown, a);

            // 但存进配置的永远是英文 Key
            Assert.Equal("Shutdown", a.Key());

            Loc.ApplyLanguage(Loc.LangEnglish);
        }
    }
}

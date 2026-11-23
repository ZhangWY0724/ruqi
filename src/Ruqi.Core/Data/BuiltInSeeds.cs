using Ruqi.Core.Models;

namespace Ruqi.Core.Data;

public static class BuiltInSeeds
{
    public static IReadOnlyList<PaymentChannel> GetDefaultPaymentChannels() =>
    [
        new() { Id = "ch_appstore", Name = "App Store", IsBuiltIn = true, SortOrder = 1 },
        new() { Id = "ch_googleplay", Name = "Google Play", IsBuiltIn = true, SortOrder = 2 },
        new() { Id = "ch_official", Name = "官网", IsBuiltIn = true, SortOrder = 3 },
        new() { Id = "ch_wechat", Name = "微信", IsBuiltIn = true, SortOrder = 4 },
        new() { Id = "ch_alipay", Name = "支付宝", IsBuiltIn = true, SortOrder = 5 },
        new() { Id = "ch_card", Name = "银行卡/信用卡", IsBuiltIn = true, SortOrder = 6 },
        new() { Id = "ch_other", Name = "其他", IsBuiltIn = true, SortOrder = 99 }
    ];

    public static IReadOnlyList<CatalogItem> GetBuiltInCatalogItems() =>
    [
        // AI 工具
        Item("cat_chatgpt_plus", "ChatGPT", BuiltInCategories.AiTools, "chatgpt", "#10A37F", "Plus", BillingCycleType.Monthly, 1, 14500, "openai gpt ai chat"),
        Item("cat_chatgpt_pro", "ChatGPT", BuiltInCategories.AiTools, "chatgpt", "#10A37F", "Pro", BillingCycleType.Monthly, 1, 145000, "openai gpt ai pro"),
        Item("cat_claude_pro", "Claude", BuiltInCategories.AiTools, "claude", "#D97757", "Pro", BillingCycleType.Monthly, 1, 14500, "anthropic ai claude"),
        Item("cat_gemini_pro", "Gemini", BuiltInCategories.AiTools, "gemini", "#4285F4", "Google AI Pro", BillingCycleType.Monthly, 1, 14500, "google ai gemini"),
        Item("cat_cursor_pro", "Cursor", BuiltInCategories.AiTools, "cursor", "#24292F", "Pro", BillingCycleType.Monthly, 1, 14500, "ide code ai cursor"),
        Item("cat_copilot_pro", "GitHub Copilot", BuiltInCategories.AiTools, "github", "#24292F", "Pro", BillingCycleType.Monthly, 1, 7200, "github copilot code ai"),
        Item("cat_midjourney", "Midjourney", BuiltInCategories.AiTools, "midjourney", "#3B4261", "Basic", BillingCycleType.Monthly, 1, 7200, "mj draw ai image"),
        Item("cat_perplexity", "Perplexity", BuiltInCategories.AiTools, "perplexity", "#20808D", "Pro", BillingCycleType.Monthly, 1, 14500, "search ai perplexity"),
        Item("cat_kimi", "Kimi", BuiltInCategories.AiTools, "kimi", "#3459F5", "会员月卡", BillingCycleType.Monthly, 1, 4900, "moonshot 月之暗面 ai"),
        Item("cat_notion_ai", "Notion AI", BuiltInCategories.AiTools, "notion", "#2F3437", "附加包", BillingCycleType.Monthly, 1, 7200, "notion ai 笔记"),

        // 影音娱乐
        Item("cat_netease_music", "网易云音乐", BuiltInCategories.Media, "netease_music", "#E60026", "黑胶 VIP", BillingCycleType.Monthly, 1, 1500, "music 网易 云音乐 黑胶"),
        Item("cat_qq_music", "QQ 音乐", BuiltInCategories.Media, "qq_music", "#31C27C", "豪华绿钻", BillingCycleType.Monthly, 1, 1500, "music qq 绿钻"),
        Item("cat_spotify", "Spotify", BuiltInCategories.Media, "spotify", "#1DB954", "个人版", BillingCycleType.Monthly, 1, 2800, "music spotify 音乐"),
        Item("cat_apple_music", "Apple Music", BuiltInCategories.Media, "apple_music", "#FA2D48", "个人版", BillingCycleType.Monthly, 1, 1100, "apple music 苹果音乐"),
        Item("cat_bilibili_m", "哔哩哔哩", BuiltInCategories.Media, "bilibili", "#FB7299", "大会员·月付", BillingCycleType.Monthly, 1, 2500, "b站 bilibili 大会员"),
        Item("cat_bilibili_y", "哔哩哔哩", BuiltInCategories.Media, "bilibili", "#FB7299", "大会员·年卡", BillingCycleType.Yearly, 1, 14800, "b站 bilibili 大会员 年费"),
        Item("cat_iqiyi", "爱奇艺", BuiltInCategories.Media, "iqiyi", "#00BE06", "黄金 VIP", BillingCycleType.Monthly, 1, 2500, "video iqiyi 爱奇艺"),
        Item("cat_tencent_video", "腾讯视频", BuiltInCategories.Media, "tencent_video", "#FF7A00", "VIP 会员", BillingCycleType.Monthly, 1, 2500, "video tencent 腾讯视频"),
        Item("cat_youku", "优酷", BuiltInCategories.Media, "youku", "#1995FF", "VIP 会员", BillingCycleType.Monthly, 1, 2500, "video youku 优酷"),
        Item("cat_mango_tv", "芒果 TV", BuiltInCategories.Media, "mango", "#FF6D00", "全屏会员", BillingCycleType.Monthly, 1, 2200, "video mango 芒果"),
        Item("cat_youtube", "YouTube Premium", BuiltInCategories.Media, "youtube", "#FF0000", "个人版", BillingCycleType.Monthly, 1, 4800, "ytb youtube 油管"),
        Item("cat_netflix", "Netflix", BuiltInCategories.Media, "netflix", "#E50914", "标准版", BillingCycleType.Monthly, 1, 6800, "nf netflix 奈飞"),

        // 云存储
        Item("cat_icloud_50", "iCloud+", BuiltInCategories.CloudStorage, "icloud", "#3B9EFF", "50GB", BillingCycleType.Monthly, 1, 600, "apple icloud 云空间"),
        Item("cat_icloud_200", "iCloud+", BuiltInCategories.CloudStorage, "icloud", "#3B9EFF", "200GB", BillingCycleType.Monthly, 1, 2100, "apple icloud 云空间"),
        Item("cat_icloud_2t", "iCloud+", BuiltInCategories.CloudStorage, "icloud", "#3B9EFF", "2TB", BillingCycleType.Monthly, 1, 6800, "apple icloud 云空间 家庭"),
        Item("cat_baidu_netdisk", "百度网盘", BuiltInCategories.CloudStorage, "baidu_pan", "#06A7FF", "超级会员 SVIP", BillingCycleType.Monthly, 1, 3000, "baidu 网盘 云盘"),
        Item("cat_aliyun_drive", "阿里云盘", BuiltInCategories.CloudStorage, "aliyun_drive", "#637DFF", "SVIP", BillingCycleType.Monthly, 1, 2500, "aliyun 阿里云盘 网盘"),
        Item("cat_quark_drive", "夸克网盘", BuiltInCategories.CloudStorage, "quark", "#3E6BFF", "SVIP", BillingCycleType.Monthly, 1, 1900, "quark 夸克 网盘"),
        Item("cat_google_one", "Google One", BuiltInCategories.CloudStorage, "google_one", "#4285F4", "100GB", BillingCycleType.Monthly, 1, 1500, "google drive one 云盘"),
        Item("cat_onedrive", "Microsoft 365", BuiltInCategories.CloudStorage, "microsoft365", "#D83B01", "个人版", BillingCycleType.Yearly, 1, 39800, "office onedrive 微软"),

        // 办公效率
        Item("cat_wps_m", "WPS", BuiltInCategories.Productivity, "wps", "#E84C3D", "超级会员·月付", BillingCycleType.Monthly, 1, 2500, "wps office 办公"),
        Item("cat_wps_y", "WPS", BuiltInCategories.Productivity, "wps", "#E84C3D", "超级会员·年卡", BillingCycleType.Yearly, 1, 17800, "wps office 办公 年卡"),
        Item("cat_notion", "Notion", BuiltInCategories.Productivity, "notion", "#2F3437", "Plus", BillingCycleType.Monthly, 1, 7200, "notion 笔记 文档"),
        Item("cat_dida", "滴答清单", BuiltInCategories.Productivity, "dida", "#4772FA", "高级会员", BillingCycleType.Yearly, 1, 13900, "ticktick dida todo 清单"),
        Item("cat_1password", "1Password", BuiltInCategories.Productivity, "onepassword", "#1A8CFF", "个人版", BillingCycleType.Monthly, 1, 2200, "password 密码"),
        Item("cat_figma", "Figma", BuiltInCategories.Productivity, "figma", "#A259FF", "Professional", BillingCycleType.Monthly, 1, 8800, "figma 设计 ui"),
        Item("cat_adobe", "Adobe Creative Cloud", BuiltInCategories.Productivity, "adobe", "#FF0000", "摄影计划", BillingCycleType.Monthly, 1, 7200, "adobe ps lr"),
        Item("cat_setapp", "Setapp", BuiltInCategories.Productivity, "setapp", "#D9A477", "标准版", BillingCycleType.Monthly, 1, 7200, "setapp mac"),
        Item("cat_weread", "微信读书", BuiltInCategories.Productivity, "weread", "#1E90FF", "付费会员", BillingCycleType.Monthly, 1, 1900, "weread 读书 阅读"),

        // 游戏会员
        Item("cat_psplus_1", "PlayStation Plus", BuiltInCategories.Gaming, "playstation", "#00439C", "基本档", BillingCycleType.Monthly, 1, 3800, "ps psn 索尼 游戏"),
        Item("cat_psplus_2", "PlayStation Plus", BuiltInCategories.Gaming, "playstation", "#00439C", "升级档", BillingCycleType.Monthly, 1, 6000, "ps psn 索尼 二档"),
        Item("cat_xgp", "Xbox Game Pass", BuiltInCategories.Gaming, "xbox", "#107C10", "Ultimate", BillingCycleType.Monthly, 1, 7900, "xbox xgp 微软 游戏"),
        Item("cat_nso", "Nintendo Switch Online", BuiltInCategories.Gaming, "nintendo", "#E60012", "个人年费", BillingCycleType.Yearly, 1, 12000, "switch nintendo 任天堂"),
        Item("cat_uu", "网易 UU 加速器", BuiltInCategories.Gaming, "uu", "#28C3A4", "高级会员", BillingCycleType.Monthly, 1, 3000, "uu 加速器 游戏"),

        // 生活购物
        Item("cat_jd_plus", "京东 PLUS", BuiltInCategories.Lifestyle, "jd", "#E1251B", "年卡", BillingCycleType.Yearly, 1, 9900, "jd 京东 购物"),
        Item("cat_88vip", "淘宝 88VIP", BuiltInCategories.Lifestyle, "taobao", "#FF5000", "年卡", BillingCycleType.Yearly, 1, 8800, "taobao 淘宝 阿里 88vip"),
        Item("cat_meituan", "美团神会员", BuiltInCategories.Lifestyle, "meituan", "#FFC300", "月卡", BillingCycleType.Monthly, 1, 1500, "meituan 外卖 美团"),
        Item("cat_eleme", "饿了么吃货卡", BuiltInCategories.Lifestyle, "eleme", "#0093FF", "月卡", BillingCycleType.Monthly, 1, 1500, "eleme 外卖 饿了么"),
        Item("cat_sams", "山姆会员", BuiltInCategories.Lifestyle, "sams", "#0067A0", "普通会员", BillingCycleType.Yearly, 1, 26000, "sams 山姆 超市"),
        Item("cat_zhihu", "知乎盐选", BuiltInCategories.Lifestyle, "zhihu", "#0066FF", "月卡", BillingCycleType.Monthly, 1, 1900, "zhihu 知乎 盐选"),
        Item("cat_keep", "Keep 会员", BuiltInCategories.Lifestyle, "keep", "#24C789", "月卡", BillingCycleType.Monthly, 1, 1900, "keep 运动 健身"),

        // 生活服务
        Item("cat_china_mobile", "中国移动", BuiltInCategories.LifeServices, "china_mobile", "#0085D0", null, BillingCycleType.OneTime, 1, null, "移动 话费 手机充值 宽带 10086"),
        Item("cat_china_unicom", "中国联通", BuiltInCategories.LifeServices, "china_unicom", "#E60027", null, BillingCycleType.OneTime, 1, null, "联通 话费 手机充值 宽带 10010"),
        Item("cat_china_telecom", "中国电信", BuiltInCategories.LifeServices, "china_telecom", "#0066B3", null, BillingCycleType.OneTime, 1, null, "电信 话费 手机充值 宽带 10000"),
        Item("cat_state_grid", "国家电网", BuiltInCategories.LifeServices, "state_grid", "#008778", null, BillingCycleType.OneTime, 1, null, "网上国网 电费 充电费 缴电费 电力 95598")
    ];

    private static CatalogItem Item(
        string id,
        string appName,
        string category,
        string iconKey,
        string brandColor,
        string? tierName,
        BillingCycleType cycleType,
        int interval,
        long? priceCents,
        string keywords) => new()
        {
            Id = id,
            IsBuiltIn = true,
            AppName = appName,
            Category = category,
            IconKey = iconKey,
            BrandColor = brandColor,
            TierName = tierName,
            DefaultCycleType = cycleType,
            DefaultInterval = interval,
            ReferencePriceCents = priceCents,
            SearchKeywords = keywords
        };
}

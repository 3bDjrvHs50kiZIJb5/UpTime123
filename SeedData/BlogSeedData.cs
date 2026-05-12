using BootstrapBlazor.Components;
using FreeSql;
using LinCms.Entities.Blog;

namespace Uptime123.SeedData;

/// <summary>
/// 博客示例种子数据 - 用于初始化博客演示数据
/// </summary>
public static class BlogSeedData
{
    /// <summary>
    /// 初始化博客示例数据
    /// </summary>
    public static void Initialize(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        if (fsql.Select<Classify>().Any() ||
            fsql.Select<SysMenu>().Any(a => new[]
            {
                "Blog/Article",
                "Blog/Comment",
                "Blog/Classify",
                "Blog/Channel",
                "Blog/Collection",
                "Blog/Tag2",
                "Blog/UserLike",
            }.Contains(a.Path)))
        {
            return;
        }

        InsertClassifies(fsql, adminUserId, adminUsername);
        InsertChannels(fsql, adminUserId, adminUsername);
        InsertTags(fsql, adminUserId, adminUsername);
        InsertChannelTags(fsql);
        InsertCollections(fsql, adminUserId, adminUsername);
        InsertArticles(fsql, adminUserId, adminUsername);
        InsertArticleCollections(fsql, adminUserId, adminUsername);
        InsertArticleTags(fsql);
        InsertComments(fsql, adminUserId, adminUsername);
        InsertUserLikes(fsql, adminUserId, adminUsername);
    }

    private static void InsertClassifies(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Classify { Id = 510337284071493, ClassifyName = "FreeSql", CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Classify { Id = 510337332621381, ClassifyName = "FreeRedis", CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Classify { Id = 510337373491269, ClassifyName = "FreeScheduler", CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Classify { Id = 510337418735685, ClassifyName = "CSRedis", CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Classify { Id = 510337460719685, ClassifyName = "Uptime123", CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertChannels(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Channel { Id = 510338108866629, ChannelName = ".NET", ChannelCode = "net", Remark = ".NET技术频道", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Channel { Id = 510338191179845, ChannelName = "前端", ChannelCode = "html", Remark = "前端技术频道", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Channel { Id = 510338291052613, ChannelName = "数据库", ChannelCode = "db", Remark = "数据库技术频道", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertTags(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Tag2 { Id = 510340412510277, TagName = "orm", Remark = "orm 文章内容", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Tag2 { Id = 510340482543685, TagName = "js", Remark = "js 有关内容", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Tag2 { Id = 510340574564421, TagName = "vue", Remark = "vue 有关内容", Status = false, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Tag2 { Id = 510340626989125, TagName = "react", Remark = "react 技术", Status = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertChannelTags(FreeSqlCloud fsql)
    {
        fsql.Insert(new[]
        {
            new Tag2.ChannelTag2 { ChannelId = 510338108866629, TagId = 510340412510277 },
            new Tag2.ChannelTag2 { ChannelId = 510338291052613, TagId = 510340412510277 },
            new Tag2.ChannelTag2 { ChannelId = 510338191179845, TagId = 510340482543685 },
            new Tag2.ChannelTag2 { ChannelId = 510338191179845, TagId = 510340574564421 },
            new Tag2.ChannelTag2 { ChannelId = 510338191179845, TagId = 510340626989125 },
        }).ExecuteAffrows();
    }

    private static void InsertCollections(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Collection { Id = 510343691022405, Name = "年度最佳", Remark = "年度精华内容", PrivacyType = PrivacyType.公开可见, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new Collection { Id = 510343769964613, Name = "月度最佳", Remark = "每月精华内容", PrivacyType = PrivacyType.仅自己可见, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertArticles(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Article
            {
                Id = 510359705468997,
                ClassifyId = 510337460719685,
                ChannelId = 510338191179845,
                Title = "国产首个支持 AOT 发布的 ORM",
                Excerpt = "FreeSql 是一款功能强大的对象关系映射组件。",
                Content = "FreeSql 是一款功能强大的对象关系映射组件，支持 CodeFirst、DbFirst、Repository、UnitOfWork、AOP，以及多种数据库。",
                IsAudit = true,
                CreatedUserId = adminUserId,
                CreatedUserName = adminUsername
            },
        }).ExecuteAffrows();
    }

    private static void InsertArticleCollections(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Article.ArticleCollection { ArticleId = 510359705468997, CollectionId = 510343769964613, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertArticleTags(FreeSqlCloud fsql)
    {
        fsql.Insert(new[]
        {
            new Tag2.TagArticle { ArticleId = 510359705468997, TagId = 510340412510277 },
            new Tag2.TagArticle { ArticleId = 510359705468997, TagId = 510340574564421 },
        }).ExecuteAffrows();
    }

    private static void InsertComments(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new Comment { Id = 510365667639365, ArticleId = 510359705468997, Text = "非常好。。。~~", IsAudit = true, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }

    private static void InsertUserLikes(FreeSqlCloud fsql, long adminUserId, string adminUsername)
    {
        fsql.Insert(new[]
        {
            new UserLike { Id = 510365571252293, SubjectId = 510359705468997, SubjectType = UserLikeSubjectType.点赞随笔, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
            new UserLike { Id = 510366600106053, SubjectId = 510365667639365, SubjectType = UserLikeSubjectType.点赞评论, CreatedUserId = adminUserId, CreatedUserName = adminUsername },
        }).ExecuteAffrows();
    }
}

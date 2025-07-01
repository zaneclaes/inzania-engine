#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public class SitePage : LogicBase { // Type type,
  protected override bool AllowRootContext => true;

  public virtual bool IncludeInSiteMap => MinimumRoleRequired <= ZUserRole.Visitor;

  public virtual ZUserRole MinimumRoleRequired => ZUserRole.Visitor;

  protected SitePage(
    SiteCategory cat, string path, string title, string? desc = null, string? author = null, params string[] keywords
  ) {
    // PageType = type;
    Template = path.Trim('/');
    List<string>? args = Template.Split(':').ToList();
    Path = args.First().Trim('/');
    args.RemoveAt(0);
    Args = args.Select(a => a.Trim('/')).ToArray();
    Title = title;
    Description = desc;
    Author = author;
    Keywords = keywords.ToList();
    Paths.Add(Path);

    Category = cat;
  }
  // public Type PageType { get; }

  [Observable] public List<string> Paths { get; } = new List<string>();

  [Observable] public string Title { get; }

  public string Path { get; }

  public string[] Args { get; }

  public string Template { get; }

  public string? Author { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }

  public SiteCategory Category { get; }
}

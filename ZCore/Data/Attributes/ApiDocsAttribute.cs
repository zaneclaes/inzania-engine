#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Class
                | AttributeTargets.Struct
                | AttributeTargets.Interface
                | AttributeTargets.Property
                | AttributeTargets.Method
                | AttributeTargets.Enum
                | AttributeTargets.Parameter
                | AttributeTargets.Field)]
public class ApiDocsAttribute : Attribute {

  public ApiDocsAttribute(string desc) { Description = desc; }

  public string Description { get; }

  public string GenerateDescription(params object[] attrObjs) {
    List<Attribute> attrs = attrObjs.Where(o => o is Attribute)
      .Where(a => !(a is ApiDocsAttribute)
        // && !(a is NullableContextAttribute) && !(a is NullableAttribute)
      )
      .Cast<Attribute>().ToList();
    // ZEnv.Log.Information("[DOCS] {desc} {@attrs}",
    //   Description, attrs.Select(a => a.GetType().Name));
    return Description;
  }
}

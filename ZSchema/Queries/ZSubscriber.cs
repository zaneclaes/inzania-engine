using System;
using System.Linq;
using System.Reflection;
using HotChocolate.Types;
using IZ.Core.Api.Types;

namespace IZ.Schema.Queries;

public static class ZSubscriber {
  // Directly hijack the HotChocolate subscribe factory to share code
  private static readonly MethodInfo SubscribeFactoryMethod =
    typeof(SubscribeAttribute).GetMethod("SubscribeFactory", BindingFlags.NonPublic | BindingFlags.Static)!;

  public static void ZSubscribe(this IObjectFieldDescriptor field, ZMethodDescriptor mi) {
    field.Extend().OnBeforeNaming((c, fieldDef) => {
      // var ctxt = c.Services.GetCurrentContext();

      // If there is no [ApiTopic] parameter, then the topic is just the method name
      string topicString = mi.Name;
      var topicParam = mi.Parameters.FirstOrDefault(p => p.IsTopic);
      if (topicParam != null) topicString += $"_{{{topicParam.FieldName}}}";

      // There must always be an [ApiEventMessage] parameter, which is the data that was broadcast
      var eventParam = mi.Parameters.FirstOrDefault(p => p.IsEventMessage) ??
                       throw new ArgumentException($"{mi.Name} has no ApiEventMessage");
      var messageType = eventParam.ParameterType;

      // Invoke the internal HotChocolate subscription method
      var factory = SubscribeFactoryMethod.MakeGenericMethod(messageType);
      factory.Invoke(null, new object?[] {
        fieldDef, topicString
      });
    });
  }

  // ----------- COPIED FROM SUBSCRIBEATTRIBUTE ---------------
/*
    private static void SubscribeFactory<TMessage>(
        ObjectFieldDefinition fieldDef,
        string topicString)
    {
        var arg = false;

        if (topicString.Contains('{'))
        {
            for (var i = 0; i < fieldDef.Arguments.Count; i++)
            {
                var argument = fieldDef.Arguments[i];
                var argumentPlaceholder = $"{{{argument.Name}}}";

                if (topicString.Contains(argumentPlaceholder))
                {
                    topicString = topicString.Replace(argumentPlaceholder, $"{{{i}}}");
                    arg = true;
                }
            }
        }

        if (arg)
        {
            fieldDef.SubscribeResolver = CreateArgumentSubscribeResolver<TMessage>(topicString);
        }
        else
        {
            fieldDef.SubscribeResolver = CreateSubscribeResolver<TMessage>(topicString);
        }
    }

    private static SubscribeResolverDelegate CreateSubscribeResolver<TMessage>(
        string topicString)
    {
        return async ctx =>
        {
            var ct = ctx.RequestAborted;
            var receiver = ctx.Service<ITopicEventReceiver>();
            return await receiver.SubscribeAsync<TMessage>(
                    topicString,
                    null,
                    null,
                    ct)
                .ConfigureAwait(false);
        };
    }

    private static SubscribeResolverDelegate CreateArgumentSubscribeResolver<TMessage>(
        string topicFormatString)
    {
        return async ctx =>
        {
            var ct = ctx.RequestAborted;
            var arguments = ctx.Selection.Field.Arguments;
            var argumentValues = new object[arguments.Count];

            // first we capture the argument values.
            for (var i = 0; i < arguments.Count; i++)
            {
                argumentValues[i] = ctx.ArgumentValue<object>(arguments[i].Name);
            }

            // next we create from it the topic string.
            var topicString = string.Format(topicFormatString, argumentValues);

            // ZEnv.Log.Information("[TOPIC] {fmt} + {vals} ({keys}) = {topic}",
            //     topicFormatString, argumentValues, ctx.Selection.Field.Arguments.Select(a => a.Name), topicString);

            // last we subscribe with the topic string.
            var receiver = ctx.Service<ITopicEventReceiver>();
            return await receiver.SubscribeAsync<TMessage>(
                    topicString,
                    null,
                    null,
                    ct)
                .ConfigureAwait(false);
        };
    }*/
}

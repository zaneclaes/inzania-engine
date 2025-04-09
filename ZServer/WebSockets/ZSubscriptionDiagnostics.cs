using System;
using HotChocolate.Subscriptions.Diagnostics;
using IZ.Core.Contexts;

namespace IZ.Server.WebSockets;

public class ZSubscriptionDiagnostics : LogicBase, ISubscriptionDiagnosticEventsListener {

  public void Created(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(Created), topicName);
  }
  public void Connected(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(Connected), topicName);
  }
  public void Disconnected(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(Disconnected), topicName);
  }
  public void MessageProcessingError(string topicName, Exception error) {
    Log.Information("[SUB] {action} {topic} {@error}", nameof(MessageProcessingError), topicName, error);
  }
  public void Received(string topicName, string serializedMessage) {
    Log.Information("[SUB] {action} {topic} {msg}", nameof(Received), topicName, serializedMessage);
  }
  public void WaitForMessages(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(WaitForMessages), topicName);
  }
  public void Dispatch<T>(string topicName, T message, int subscribers) {
    Log.Information("[SUB] {action} {topic} {msg} to {count}", nameof(Dispatch), topicName, message, subscribers);
  }
  public void TrySubscribe(string topicName, int attempt) {
    Log.Information("[SUB] {action} {topic} {count}", nameof(TrySubscribe), topicName, attempt);
  }
  public void SubscribeSuccess(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(SubscribeSuccess), topicName);
  }
  public void SubscribeFailed(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(SubscribeFailed), topicName);
  }
  public void Unsubscribe(string topicName, int shard, int subscribers) {
    Log.Information("[SUB] {action} {topic}", nameof(Unsubscribe), topicName);
  }
  public void Close(string topicName) {
    Log.Information("[SUB] {action} {topic}", nameof(Close), topicName);
  }
  public void Send<T>(string topicName, T message) {
    Log.Information("[SUB] {action} {topic} {msg}", nameof(Send), topicName, message);
  }
  public void ProviderInfo(string infoText) {
    Log.Information("[SUB] {action} {info}", nameof(ProviderInfo), infoText);
  }
  public void ProviderTopicInfo(string topicName, string infoText) {
    Log.Information("[SUB] {action} {topic} {info}", nameof(ProviderTopicInfo), topicName, infoText);
  }

  public ZSubscriptionDiagnostics(IZContext context) : base(context) {  }
}

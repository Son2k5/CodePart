---
kind: external_dependency
name: In-process event bus via MediatR INotification
slug: mediatr
category: external_dependency
category_hints:
    - framework_behavior
scope:
    - '**'
---

Cross-module triggers use MediatR's `INotification` pattern (not IRequest): modules publish events via `IPublisher`, and handlers implement `INotificationHandler<T>` to react. The Application layer auto-registers handlers from its assembly and wires `LoggingBehavior` + `ValidationBehavior` globally. This is the existing integration-event mechanism the Notification module should extend, not replace.
---
kind: external_dependency
name: Email delivery via MailKit SMTP
slug: mailkit
category: external_dependency
category_hints:
    - framework_behavior
scope:
    - '**'
---

Email sending goes through `IEmailSender` implemented by `EmailSender` using `MailKit.Net.Smtp.SmtpClient`. Templates are built inline with `BodyBuilder` (no Razor/Scriban). Configuration lives under `Email.Smtp` (`Host`, `Port`, `Username`, `Password`, `FromAddress`, `FromName`, `SocketOptions`). Any new notification channel should reuse this abstraction rather than instantiating SMTP directly.
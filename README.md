Log4Http
===

Log4Http is a simple http appender for log4net. It merges all log messages internally and sends them in http requests by an intervall.

Use the %messages placeholder inside the content to get the merged messages.

Example Configuration:
===
```
  <appender name="HttpAppender" type="Log4Http.HttpAppender, Log4Http">
    <url value="https://slack.com/api/files.upload" />
    <header>
      <name>Content-Type</name>
      <value>application/x-www-form-urlencoded</value>
    </header>
    <content value="token=XXX&amp;content=%messages&amp;channels=XXXXXX&amp;title=Log-Report"/>
    <sendIntervalMinutes value="1" />
    <debugMode value="true" />
    
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date{dd.MM.yyyy HH:mm:ss} %-5level %logger - %message%newline" />
    </layout>
  </appender>
```
# Files, Channels And Change Logging

`FilesManager` coordinates domain file metadata with storage providers from `Grammophone.Storage`. It uploads, downloads, reads and schedules deletion of file contents while keeping metadata in the domain container.

Channel abstractions support asynchronous notifications. `IChannel<T>` sends typed messages, `TaskChannelsDispatcher<T>` forwards messages to registered channels and `EmailChannel<T>` is a base for email-backed channels.

Change-logging abstractions include `IEntityChangeLogger<TUser>`, `PropertyState`, `EntityChangeLogDeserializer` and `JsonEntityChangeLogDeserializer`. These APIs support recording and reconstructing entity state changes.

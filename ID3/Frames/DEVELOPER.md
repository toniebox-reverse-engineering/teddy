# Developer Notes
Frames classes are the in-memory object representations of ID3 frames. There are agnostic to specific ID3 versions, and hence do not have the knowledge to serialize or deserialize themselves from an ID3 tag; this is the responsibility of the ID3 handler classes.

## Base classes

### `Id3Frame`
* Base class for all frames
* Has an `IsAssigned` abstract boolean property, which indicates whether the frame has valid data.
    * Because of the lazy-loaded nature of frame properties in the `Id3Tag` class, it is possible for frame instances to be created, but not have valid data yet. Simply accessing a property will create an instance if one doesn't already exist. This was done to prevent `NullReferenceException` exceptions and unnecessary checking for `null` values.
    * When saving ID3 tags to MP3 streams, only assigned frames are saved.

### `TextFrameBase<TValue>`
* Base class for all frames that are classified as text frames (from the ID3 v2.3 spec, any of the `T???` frames).
* Some text frame implementations may not derive from this class if they have special behaviors or requirements.
* Has a `Value` property that is typed as `TValue`, which represents the primary text data, but as a specific type. Internally, a specific implementation must override the `TextValue` property to customize how to serialize and deserialize the `TValue` value as a string.
* Several specialized base classes inherit from this class for each concrete type of `TValue`.
    * `TextFrame : TextFrameBase<string>`: Base class for text frames where the primary data is a string
    * `NumericFrame : TextFrameBase<int?>`: Base class for text frames where the primary data is a number
    * `DateTimeFrame : TextFrameBase<DateTime?>`: Base class for text frames where the primary data is a `DateTime`
    * `ListTextFrame : TextFrameBase<IList<string>>`: Base class for text frames where the primary data is a `/`-separated string and is surfaced as an `IList<string>`

### `UrlLinkFrame`
* Base class for all frames that store URL information (from the ID3 v2.3 spec, any of the `W???` frames).
* Has a `Url` property that represents a single URL.

## When adding new frame types
The following areas need to be updated:
* `Id3.Net`
    * Add a property for it in the `Id3Tag` class
    * Handle it in all available ID3 handlers.
    * If the frame is a multi-instance frame, register it with the `Id3Tag.MultiInstanceFrameTypes` private static field.
* `Id3.Net.Serialization`: Add a serialization surrogate for the frame (or reuse an existing one) and register it in the `SerializationExtensions.IncludeId3SerializationSupport` method.
* `Id3.Net.Files`: Check whether the new frame can be used as a placeholder in a file naming pattern for the `FileNamer` class. If so, add it to the `FileNamer._allowedNames` static field.

## Implementation characteristics of a frame class
* Must have a default constructor, for serialization purposes.
* Must have one or more constructor overrides to set key properties
* If the frame has one value that can be considered its default value:
    * Must have a constructor overload to set just that value
    * Must have implicit cast operators to convert the frame instance to the default value and vice versa
* Must implement `IEquatable`, which will compare only data properties for equality

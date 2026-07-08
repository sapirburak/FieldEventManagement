using System;

namespace FieldEventManagement.Application.DTOs;

// העטיפה המלאה שה-Agent שולח, הכוללת את ה-Guid הייחודי
public record WrappedEvent(
    Guid Id,
    FieldEventDto Data
);
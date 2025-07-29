namespace WriteFluency.Infrastructure.ExternalApis;

public record TextToSpeechRequest(
    Input Input,
    AudioConfig AudioConfig,
    Voice Voice
);

public record Input(
    string Text
);

public record AudioConfig(
    string AudioEncoding
);

public record Voice(
    string LanguageCode
);

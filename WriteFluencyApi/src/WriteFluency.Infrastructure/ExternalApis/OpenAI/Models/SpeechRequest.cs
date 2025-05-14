namespace WriteFluency.Infrastructure.ExternalApis;

public record SpeechRequest(
    string Model,
    string Input,
    string Voice
);

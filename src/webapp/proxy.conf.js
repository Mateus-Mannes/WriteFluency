module.exports = {
  "/api": {
    target:
      process.env["services__api__https__0"] ||
      process.env["services__api__http__0"],
    secure: false,
    pathRewrite: {
      "^/api": "",
    },
  },
  "/v1": {
    target: process.env["OTEL_EXPORTER_OTLP_ENDPOINT"] || "http://localhost:4318",
    secure: false,
    pathRewrite: {
      "^/v1": "",
    },
  }
};
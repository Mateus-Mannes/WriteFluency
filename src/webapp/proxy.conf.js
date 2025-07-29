module.exports = {
  "/api": {
    target:
      process.env["services__wf-api__https__0"] ||
      process.env["services__wf-api__http__0"],
    secure: false,
    pathRewrite: {
      "^/api": "",
    },
  }
};
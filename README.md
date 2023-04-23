# tandoku
tandoku is a Japanese reading system comprising apps and utilities to help read and learn from native Japanese content.

## Repo organization
| Path           | Description                                                                               |
|----------------|-------------------------------------------------------------------------------------------|
| docs           | Documentation, includes workflow definitions                                              |
| scripts        | Scripts implementing prototypes of tandoku commands, file names like TandokuVolumeNew.ps1 |
| scripts/common | Modules and scripts included in other top-level scripts via using or dot sourcing         |
| src            | Source code for production tandoku libraries and tools                                    |
| tests          | Test code for tandoku libraries and tools                                                 |

## Testing
Some of the tests leverage the [Verify](https://github.com/VerifyTests/Verify) snapshot tool. For the best experience
updating test snapshots, install a supported diff tool and/or the dotnet global tool [verify.tool](https://github.com/VerifyTests/Verify.Terminal).
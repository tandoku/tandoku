# prereqs:
# scoop install graphviz
# install-module psgraph

param(
    [Parameter(ValueFromPipeline=$true)]
    [String[]]
    $Path,

    [Parameter()]
    [String]
    $OutFile,

    [Parameter()]
    [Switch]
    $ShowGraph
)

function BuildTandokuWorkflowGraph($Path) {
    $wfDocs = Get-ChildItem $Path |
        ForEach-Object {
            Get-Content $_ | ConvertFrom-Yaml -AllDocuments
        }

    graph workflow @{fontname='Helvetica'} {
        node @{fontname='Helvetica'; fontcolor='white'}

        foreach ($wf in $wfDocs) {
            Inline "# stage: $($wf.stage), media: $($wf.media)"

            # artifacts
            if ($wf.artifacts) {
                node @{shape='rect'; style="filled,rounded"; fillcolor='orange'}
                $wf.artifacts.keys | ForEach-Object {node $_}
            }

            # values
            if ($wf.simpleValues) {
                node @{shape='rect'; style="filled,rounded"; fillcolor='green'}
                $wf.simpleValues.keys | ForEach-Object {node $_}
            }

            # operations
            if ($wf.operations) {
                node @{shape = 'rect'; style = "filled"; fillcolor = 'blue' }
                $wf.operations.keys | ForEach-Object {
                    $o = $wf.operations[$_]
                    if ($o.inputs) {
                        edge $o.inputs $_
                    }
                    if ($o.outputs) {
                        edge $_ $o.outputs
                    }
                }
            }
        }
    }
}

if (-not $Path) {
    $Path = ".\*.tdkw.yaml"
}

$graph = BuildTandokuWorkflowGraph $Path

if ($OutFile) {
    $graph | Set-Content $OutFile
}

if ($ShowGraph) {
    # TODO: find dot.exe path dynamically (Get-Command?)
    # TODO: add option to cleanup temp files (rm $env:temp\*.*.svg)
    $graph | Show-PSGraph -GraphVizPath ~\scoop\shims\dot.exe -OutputFormat svg
}

if (-not $OutFile -and -not $ShowGraph) {
    $graph
}
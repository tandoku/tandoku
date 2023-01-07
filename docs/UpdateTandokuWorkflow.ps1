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

function BuildTandokuWorkflowGraph($wfDocs) {
    graph workflow @{fontname='Helvetica'} {
        node @{fontname='Helvetica'; fontcolor='white'; penwidth="0.2"}
        edge @{fontname="Helvetica"; arrowsize="0.6"}

        foreach ($wf in $wfDocs) {
            Inline "# stage: $($wf.stage), media: $($wf.media)"

            # TODO: subgraphs based on different criteria ?
            #subgraph {
                # artifacts
                if ($wf.artifacts) {
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = 'orange' }
                    AddNodesFromKeys $wf.artifacts
                }

                # values
                if ($wf.simpleValues) {
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = 'green' }
                    AddNodesFromKeys $wf.simpleValues
                }

                # operations
                if ($wf.operations) {
                    node @{shape = 'rect'; style = "filled"; fillcolor = 'blue' }
                    AddNodesFromKeys $wf.operations

                    # undefined nodes (created by edges) should be values
                    # TODO: share with values above
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = 'green' }

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
            #}
        }
    }
}

function AddNodesFromKeys($map) {
    $map.keys | ForEach-Object {
        $o = $map[$_]
        $attrs = @{}
        if ($o.summary) { $attrs.tooltip = $o.summary }
        node -Name $_ -Attributes $attrs
    }
}

function BuildTandokuArtifactsTable($wfDocs) {
    $wfDocs | ForEach-Object {
        $wf = $_

        if ($wf.artifacts) {
            foreach ($k in $wf.artifacts.keys) {
                $a = $wf.artifacts[$k]
                [PSCustomObject] @{
                    name = $k
                    container = $a.container
                    stage = $wf.stage
                    media = $wf.media
                    location = $a.location
                    sourceControl = $a.sourceControl
                    summary = $a.summary
                }
            }
        }
    } | Export-Csv '.\tandoku-workflow-artifacts.csv'
}

function GetTandokuWorkflowDocs($Path) {
    return Get-ChildItem $Path |
        ForEach-Object {
            Get-Content $_ | ConvertFrom-Yaml -AllDocuments
        }
}

if (-not $Path) {
    $Path = ".\*.tdkw.yaml"
}

$wfDocs = GetTandokuWorkflowDocs $Path

$graph = BuildTandokuWorkflowGraph $wfDocs

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

# TODO: rationalize the parameters to allow building graph, tables or both
BuildTandokuArtifactsTable $wfDocs
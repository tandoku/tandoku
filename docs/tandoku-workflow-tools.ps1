# prereqs:
# scoop install graphviz
# install-module psgraph

function psgr {
    $input|Show-PSGraph @Args -GraphVizPath ~\scoop\shims\dot.exe
}

function BuildTandokuWorkflowGraph {
    $wf = Get-Content .\tandoku-workflow.yaml | ConvertFrom-Yaml -AllDocuments

    graph workflow @{fontname='Helvetica'} {
        node @{fontname='Helvetica'}

        $wf.artifacts.keys | ForEach-Object {node $_}

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

import type { Stub } from '@/lib/api'

// A URL-path tree of stubs the way the WireMock UI groups them: each path segment is a folder, and the
// deepest folder is the endpoint. An endpoint holds its stubs grouped by HTTP method, because one
// URL+method can back many stubs — a 200 case, a 404 case, different request-match variants. So the
// hierarchy is: path segments → method → cases (each a single stub, shown by name + status code).

export interface StubTreeNode {
  groups: Map<string, StubTreeNode>   // child path segments (sub-resources)
  methods: Map<string, Stub[]>        // stubs whose URL ends at this node, bucketed by method
}

const emptyNode = (): StubTreeNode => ({ groups: new Map(), methods: new Map() })

export function buildStubTree(stubs: Stub[]): StubTreeNode {
  const root = emptyNode()
  for (const stub of stubs) {
    const segments = stub.url.replace(/^\//, '').split('/').filter(Boolean)
    let node = root
    for (const seg of segments) {
      let child = node.groups.get(seg)
      if (!child) { child = emptyNode(); node.groups.set(seg, child) }
      node = child
    }
    const list = node.methods.get(stub.method)
    if (list) list.push(stub)
    else node.methods.set(stub.method, [stub])
  }
  return root
}

/** Total stubs at/under a node (all method buckets + descendants) — for a folder's count badge. */
export function countStubs(node: StubTreeNode): number {
  let n = 0
  for (const list of node.methods.values()) n += list.length
  for (const child of node.groups.values()) n += countStubs(child)
  return n
}

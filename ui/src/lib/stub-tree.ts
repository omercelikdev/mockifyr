import type { Stub } from '@/lib/api'

// A URL-path tree of stubs, the way Postman / the WireMock UI group requests: each path segment is a
// folder, the last segment is the stub. Stubs sharing a folder nest under it; a leaf's label is the
// stub's own name when set, otherwise its last segment.

export interface StubTreeNode {
  groups: Map<string, StubTreeNode>
  leaves: { label: string; stub: Stub }[]
}

const emptyNode = (): StubTreeNode => ({ groups: new Map(), leaves: [] })

export function buildStubTree(stubs: Stub[]): StubTreeNode {
  const root = emptyNode()
  for (const stub of stubs) {
    const segments = stub.url.replace(/^\//, '').split('/').filter(Boolean)
    let node = root
    for (let i = 0; i < segments.length - 1; i++) {
      const seg = segments[i]
      let child = node.groups.get(seg)
      if (!child) { child = emptyNode(); node.groups.set(seg, child) }
      node = child
    }
    const last = segments[segments.length - 1] ?? stub.url
    node.leaves.push({ label: stub.name?.trim() || last, stub })
  }
  return root
}

/** Total stubs under a node (all descendant leaves) — for a group's count badge. */
export function countLeaves(node: StubTreeNode): number {
  let n = node.leaves.length
  for (const child of node.groups.values()) n += countLeaves(child)
  return n
}

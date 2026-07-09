// Reference catalog for the in-app "Templating helpers" popup (#120). These document the Handlebars
// helpers Mockifyr supports for mapping request values into response bodies/headers. Entries mirror the
// behaviours proven in docs/parity; the popup is a lookup aid, not an executable surface.

export interface Helper {
  name: string
  syntax: string
  desc: string
  example?: string
  output?: string
}

export interface HelperCategory {
  key: string
  label: string
  helpers: Helper[]
}

export const TEMPLATING_HELPERS: HelperCategory[] = [
  {
    key: 'request', label: 'Request model',
    helpers: [
      { name: 'request.body', syntax: '{{request.body}}', desc: 'The raw request body as a string.' },
      { name: 'request.method', syntax: '{{request.method}}', desc: 'The HTTP method.', example: 'POST' },
      { name: 'request.url', syntax: '{{request.url}}', desc: 'The full request URL incl. query string.' },
      { name: 'request.path', syntax: '{{request.path.[0]}}', desc: 'A path segment by index (0-based).', example: '/api/users → {{request.path.[1]}}', output: 'users' },
      { name: 'request.path (named)', syntax: '{{request.path.id}}', desc: 'A named path variable from a urlPathTemplate match.' },
      { name: 'request.query', syntax: '{{request.query.page}}', desc: 'A query parameter by name.', example: '?page=3', output: '3' },
      { name: 'request.headers', syntax: '{{request.headers.[Content-Type]}}', desc: 'A request header by name (bracket names with symbols).' },
      { name: 'request.cookies', syntax: '{{request.cookies.session}}', desc: 'A request cookie by name.' },
    ],
  },
  {
    key: 'extraction', label: 'Data extraction',
    helpers: [
      { name: 'jsonPath', syntax: "{{jsonPath request.body '$.user.id'}}", desc: 'Extract a value from a JSON body with a JSONPath expression.', example: '{"user":{"id":42}}', output: '42' },
      { name: 'xPath', syntax: "{{xPath request.body '/order/id/text()'}}", desc: 'Extract a value from an XML body with an XPath expression.' },
      { name: 'soapXPath', syntax: "{{soapXPath request.body '/Envelope/Body/id'}}", desc: 'XPath scoped to a SOAP envelope body.' },
    ],
  },
  {
    key: 'json', label: 'JSON helpers',
    helpers: [
      { name: 'parseJson (inline)', syntax: "{{#parseJson 'o' request.body}}{{o.total}}{{/parseJson}}", desc: 'Parse a JSON string into a variable, then read fields off it.' },
      { name: 'toJson', syntax: '{{{toJson someObject}}}', desc: 'Serialize a value back to JSON (triple-stash to skip HTML-escaping).' },
    ],
  },
  {
    key: 'string', label: 'String helpers',
    helpers: [
      { name: 'upper / lower', syntax: '{{upper x}} · {{lower x}}', desc: 'Upper- or lower-case a string.' },
      { name: 'capitalize', syntax: '{{capitalize x}}', desc: 'Capitalize the first letter.' },
      { name: 'trim', syntax: '{{trim x}}', desc: 'Strip leading/trailing whitespace.' },
      { name: 'substring', syntax: '{{substring x 0 4}}', desc: 'A substring by start/end index.', example: 'abcdef', output: 'abcd' },
      { name: 'replace', syntax: "{{replace x 'a' 'b'}}", desc: 'Replace occurrences of a substring.' },
      { name: 'join', syntax: "{{join items ', '}}", desc: 'Join an array into a string with a separator.' },
    ],
  },
  {
    key: 'number', label: 'Number & math',
    helpers: [
      { name: 'add / subtract', syntax: '{{add a b}} · {{subtract a b}}', desc: 'Arithmetic on two numbers.' },
      { name: 'multiply / divide', syntax: '{{multiply a b}} · {{divide a b}}', desc: 'Multiply or divide two numbers.' },
      { name: 'round', syntax: '{{round x}}', desc: 'Round to the nearest integer.', example: '3.6', output: '4' },
      { name: 'abs', syntax: '{{abs x}}', desc: 'Absolute value.' },
    ],
  },
  {
    key: 'random', label: 'Random values',
    helpers: [
      { name: 'randomValue (UUID)', syntax: "{{randomValue type='UUID'}}", desc: 'A random UUID.' },
      { name: 'randomInt', syntax: '{{randomInt lower=1 upper=100}}', desc: 'A random integer in a range.' },
      { name: 'faker', syntax: "{{random 'Name.fullName'}}", desc: 'A fake value from the Faker catalog (names, addresses, etc.).', example: "{{random 'Internet.email'}}" },
    ],
  },
  {
    key: 'datetime', label: 'Date & time',
    helpers: [
      { name: 'now', syntax: '{{now}}', desc: 'The current timestamp (ISO-8601 by default).' },
      { name: 'now (format)', syntax: "{{now format='yyyy-MM-dd'}}", desc: 'The current time in a custom format.' },
      { name: 'now (offset)', syntax: "{{now offset='3 days'}}", desc: 'Shift the current time by an offset.' },
    ],
  },
  {
    key: 'encoding', label: 'Encoding',
    helpers: [
      { name: 'base64', syntax: '{{base64 x}}', desc: 'Base64-encode a string.' },
      { name: 'base64 (decode)', syntax: '{{base64 x decode=true}}', desc: 'Base64-decode a string.' },
      { name: 'urlEncode / urlDecode', syntax: '{{urlEncode x}} · {{urlDecode x}}', desc: 'Percent-encode or decode a string.' },
      { name: 'jwt', syntax: "{{jwt alg='HS256' secret='…' payload=…}}", desc: 'Sign a JWT from a payload.' },
    ],
  },
  {
    key: 'conditionals', label: 'Conditionals',
    helpers: [
      { name: 'if / else', syntax: '{{#if x}}yes{{else}}no{{/if}}', desc: 'Branch on a truthy value.' },
      { name: 'eq', syntax: "{{#eq a 'ok'}}…{{/eq}}", desc: 'Equality comparison block.' },
      { name: 'gt / lt', syntax: '{{#gt a b}}…{{/gt}}', desc: 'Greater-than / less-than comparison block.' },
      { name: 'unless', syntax: '{{#unless x}}…{{/unless}}', desc: 'Inverse of if.' },
    ],
  },
  {
    key: 'iteration', label: 'Iteration',
    helpers: [
      { name: 'each', syntax: '{{#each items}}{{this}}{{/each}}', desc: 'Iterate over an array or object.' },
      { name: 'each (index)', syntax: '{{#each items}}{{@index}}:{{this}}{{/each}}', desc: 'Iterate with the current index via @index.' },
    ],
  },
]

// Performance measurement on the main DB: explain('executionStats') for the queries of both endpoints.
// The sort includes an _id tiebreaker, like the application. To run (see README):
//   docker cp scripts/perf-explain.js requests-mongo:/tmp/perf-explain.js
//   docker exec requests-mongo mongosh --quiet --file /tmp/perf-explain.js
const dbx = db.getSiblingDB('requestsManagement');
const col = dbx.requests;

function stages(plan) {
  const out = [];
  (function walk(p) {
    if (!p) return;
    out.push(p.indexName ? `${p.stage}(${p.indexName})` : p.stage);
    walk(p.inputStage);
    (p.inputStages || []).forEach(walk);
  })(plan);
  return out.join(' <- ');
}

function find(label, filter, sort, skip, limit) {
  const e = col.find(filter).sort(sort).skip(skip).limit(limit).explain('executionStats');
  const s = e.executionStats;
  print(`FIND | ${label} | ${stages(e.queryPlanner.winningPlan)} | returned=${s.nReturned} keys=${s.totalKeysExamined} docs=${s.totalDocsExamined} ms=${s.executionTimeMillis}`);
}

function count(label, filter) {
  const e = col.explain('executionStats').count(filter);
  const s = e.executionStats;
  print(`COUNT | ${label} | ${stages(e.queryPlanner.winningPlan)} | keys=${s.totalKeysExamined} docs=${s.totalDocsExamined} ms=${s.executionTimeMillis}`);
}

function agg(label, pipeline) {
  const e = col.explain('executionStats').aggregate(pipeline);
  const st = e.stages ? e.stages[0].$cursor : e;
  const ex = st.executionStats;
  const plan = st.queryPlanner ? stages(st.queryPlanner.winningPlan) : 'n/a';
  print(`AGG | ${label} | ${plan} | keys=${ex.totalKeysExamined} docs=${ex.totalDocsExamined} ms=${ex.executionTimeMillis}`);
}

print('total=' + col.estimatedDocumentCount());
print('indexes=' + col.getIndexes().map(i => i.name).join(','));

find('default: sort CreatedAt desc, page 1', {}, { CreatedAt: -1, _id: -1 }, 0, 25);
find('status+priority, sort CreatedAt desc', { Status: 3, Priority: 2 }, { CreatedAt: -1, _id: -1 }, 0, 25);
count('status+priority', { Status: 3, Priority: 2 });
find('organization, sort CreatedAt desc', { OrganizationName: 'רשות המסים' }, { CreatedAt: -1, _id: -1 }, 0, 25);
find('createdAt range (6 months)', { CreatedAt: { $gte: ISODate('2026-01-01'), $lte: ISODate('2026-06-30T23:59:59Z') } }, { CreatedAt: -1, _id: -1 }, 0, 25);
find('text search "דחופה", sort CreatedAt desc', { $text: { $search: 'דחופה' } }, { CreatedAt: -1, _id: -1 }, 0, 25);
find('sort Priority asc', {}, { Priority: 1, _id: 1 }, 0, 25);
find('sort Priority desc', {}, { Priority: -1, _id: -1 }, 0, 25);
find('sort Status asc', {}, { Status: 1, _id: 1 }, 0, 25);
find('sort Title asc', {}, { Title: 1, _id: 1 }, 0, 25);
find('sort UpdatedAt desc', {}, { UpdatedAt: -1, _id: -1 }, 0, 25);
find('sort OrganizationName asc', {}, { OrganizationName: 1, _id: 1 }, 0, 25);
find('deep page: skip 50,000', {}, { CreatedAt: -1, _id: -1 }, 50000, 25);

agg('summary: group by Status', [{ $group: { _id: '$Status', count: { $sum: 1 } } }]);
agg('summary: group by Priority', [{ $group: { _id: '$Priority', count: { $sum: 1 } } }]);

// Stable paging: 40 pages by Priority must yield 1000 unique ids.
const seen = new Set();
let rows = 0;
for (let p = 0; p < 40; p++) {
  col.find({}, { _id: 1 }).sort({ Priority: 1, _id: 1 }).skip(p * 25).limit(25).forEach(d => { seen.add(d._id.toString()); rows++; });
}
print(`STABILITY | sort Priority + _id, 40 pages x 25 | rows=${rows} unique=${seen.size} duplicates=${rows - seen.size}`);

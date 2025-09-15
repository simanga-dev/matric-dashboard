
function csvToJson(csvString: string) {
  const rows = csvString.split("\n");
  const headers = rows[0].split(",");
  const jsonData = [];
  for (let i = 1; i < rows.length - 1; i++) {
    const values = rows[i].split(",");
    const obj = {};
    for (let j = 0; j < headers.length; j++) {
      const key = headers[j].trim();
      const value = values[j].trim();
      obj[key] = value;
    }
    obj.id = i;
    jsonData.push(obj);
  }
  return JSON.stringify(jsonData);
}

let csv = await Bun.file(process.argv[2]).text();

const jsonData = csvToJson(csv);

const url = "http://0.0.0.0:7700/indexes/schools/documents?primaryKey=id";

var x = await fetch(url, {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    Authorization: "Bearer " + process.env.MEILI_TOKEN,
  },
  body: jsonData,
})
  .then((response) => response.json())
  .then((data) => console.log(data))
  .catch((error) => console.error("Error:", error));

console.log(x);

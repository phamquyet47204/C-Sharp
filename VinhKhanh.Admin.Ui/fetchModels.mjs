import axios from 'axios';
async function run() {
  const res = await axios.get('https://generativelanguage.googleapis.com/v1beta/models?key=AIzaSyCOvjUngEyKNEkZhwBgWBnTKxq_PqZOxxU');
  const items = res.data.models.map(m => m.name).filter(n => n.includes('gemini'));
  console.log(items);
}
run();

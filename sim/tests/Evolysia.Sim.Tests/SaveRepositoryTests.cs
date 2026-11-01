using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class SaveRepositoryTests
{
    static void InFolder(Action<string> action)
    {
        string folder = Path.Combine(Path.GetTempPath(), "evolysia-slots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { action(folder); }
        finally { foreach (string file in Directory.EnumerateFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }

    [Fact]
    public void Named_slots_load_independently_and_deleting_one_preserves_the_others()
    {
        InFolder(folder =>
        {
            var repository = new SaveRepository(folder); var sim = CognitionTests.World();
            var first = repository.SaveNew(sim, "River people"); var second = repository.SaveNew(sim, "Mountain people");
            Assert.Equal(2, repository.List().Count); Assert.Equal(sim.Config.Seed, repository.Load(first.Id).Config.Seed);
            Assert.True(repository.Delete(first.Id)); Assert.Equal(second.Id, Assert.Single(repository.List()).Id);
            Assert.Equal(sim.Living.Count, repository.Load(second.Id).Living.Count);
        });
    }

    [Fact]
    public void Autosaves_rotate_without_replacing_manual_or_quick_saves()
    {
        InFolder(folder =>
        {
            var repository = new SaveRepository(folder); var sim = CognitionTests.World();
            var manual = repository.SaveNew(sim, "Keep this one"); repository.Save(sim, "quick", "Quick save");
            for (int i = 0; i < 8; i++) repository.Autosave(sim, 3);
            var slots = repository.List(); Assert.Equal(5, slots.Count); Assert.Equal(3, slots.Count(s => s.Automatic));
            Assert.Contains(slots, s => s.Id == manual.Id && !s.Automatic); Assert.Contains(slots, s => s.Id == "quick");
            Assert.Equal(10, Directory.EnumerateFiles(folder).Count());
        });
    }

    [Fact]
    public void Invalid_names_and_corrupt_metadata_do_not_escape_the_save_folder_or_block_valid_worlds()
    {
        InFolder(folder =>
        {
            var repository = new SaveRepository(folder); var sim = CognitionTests.World();
            Assert.Throws<ArgumentException>(() => repository.Save(sim, "../outside", "No"));
            Assert.Throws<ArgumentException>(() => repository.Save(sim, "auto-0", "Reserved"));
            Assert.Throws<ArgumentException>(() => repository.Save(sim, "valid", ""));
            repository.Save(sim, "valid", "World"); File.WriteAllText(Path.Combine(folder, "bad.json"), "{");
            Assert.Single(repository.List()); Assert.Equal(sim.Living.Count, repository.Load("valid").Living.Count);
            Assert.Throws<ArgumentException>(() => repository.Delete("../../outside"));
        });
    }
}

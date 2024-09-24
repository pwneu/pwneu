﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pwneu.Play.Shared.Data;

#nullable disable

namespace Pwneu.Play.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20240923154248_AddHintUsageUserName")]
    partial class AddHintUsageUserName
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Artifact", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ChallengeId")
                        .HasColumnType("uuid");

                    b.Property<string>("ContentType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<byte[]>("Data")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("ChallengeId");

                    b.ToTable("Artifacts");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Category", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("Categories");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Challenge", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CategoryId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("Deadline")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("DeadlineEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<List<string>>("Flags")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<int>("MaxAttempts")
                        .HasColumnType("integer");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<int>("SolveCount")
                        .HasColumnType("integer");

                    b.Property<List<string>>("Tags")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.ToTable("Challenges");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Hint", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ChallengeId")
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("Deduction")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("ChallengeId");

                    b.ToTable("Hints");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.HintUsage", b =>
                {
                    b.Property<string>("UserId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)");

                    b.Property<Guid>("HintId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("UsedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.HasKey("UserId", "HintId");

                    b.HasIndex("HintId");

                    b.ToTable("HintUsages");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.PlayConfiguration", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Key");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("PlayConfigurations");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Submission", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ChallengeId")
                        .HasColumnType("uuid");

                    b.Property<string>("Flag")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsCorrect")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("SubmittedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.HasKey("Id");

                    b.HasIndex("ChallengeId");

                    b.ToTable("Submissions");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Artifact", b =>
                {
                    b.HasOne("Pwneu.Play.Shared.Entities.Challenge", "Challenge")
                        .WithMany("Artifacts")
                        .HasForeignKey("ChallengeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Challenge");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Challenge", b =>
                {
                    b.HasOne("Pwneu.Play.Shared.Entities.Category", "Category")
                        .WithMany("Challenges")
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Category");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Hint", b =>
                {
                    b.HasOne("Pwneu.Play.Shared.Entities.Challenge", "Challenge")
                        .WithMany("Hints")
                        .HasForeignKey("ChallengeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Challenge");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.HintUsage", b =>
                {
                    b.HasOne("Pwneu.Play.Shared.Entities.Hint", "Hint")
                        .WithMany("HintUsages")
                        .HasForeignKey("HintId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Hint");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Submission", b =>
                {
                    b.HasOne("Pwneu.Play.Shared.Entities.Challenge", "Challenge")
                        .WithMany("Submissions")
                        .HasForeignKey("ChallengeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Challenge");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Category", b =>
                {
                    b.Navigation("Challenges");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Challenge", b =>
                {
                    b.Navigation("Artifacts");

                    b.Navigation("Hints");

                    b.Navigation("Submissions");
                });

            modelBuilder.Entity("Pwneu.Play.Shared.Entities.Hint", b =>
                {
                    b.Navigation("HintUsages");
                });
#pragma warning restore 612, 618
        }
    }
}

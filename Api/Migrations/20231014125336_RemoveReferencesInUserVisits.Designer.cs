﻿// <auto-generated />
using System;
using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Api.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20231014125336_RemoveReferencesInUserVisits")]
    partial class RemoveReferencesInUserVisits
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.11");

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.HikingTour", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Endpoint")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsCircularPath")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsKidsTour")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsLongDistanceTrail")
                        .HasColumnType("INTEGER");

                    b.Property<string>("KomootUri")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Startpoint")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("HikingTours");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.Import", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Date")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasDefaultValueSql("datetime('now')");

                    b.Property<int>("HikingToursCount")
                        .HasColumnType("INTEGER");

                    b.Property<int>("StampingPointsCount")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Imports");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.SortedStampingPoint", b =>
                {
                    b.Property<int>("Position")
                        .HasColumnType("INTEGER")
                        .HasColumnOrder(0);

                    b.Property<int>("StampingPointId")
                        .HasColumnType("INTEGER")
                        .HasColumnOrder(1);

                    b.Property<int>("TourId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Position", "StampingPointId", "TourId");

                    b.HasIndex("TourId");

                    b.ToTable("SortedStampingPoint", (string)null);
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.StampingPoint", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Code")
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Latitude")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("Longitude")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Number")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("StampingPoints");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.UserVisit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("EntryCreated")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasDefaultValueSql("datetime('now')");

                    b.Property<int>("StampingPointId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("Visited")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("UserVisit", (string)null);
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.SortedStampingPoint", b =>
                {
                    b.HasOne("TourEd.Lib.Abstractions.Models.HikingTour", "Tour")
                        .WithMany("StampingPoints")
                        .HasForeignKey("TourId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Tour");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.UserVisit", b =>
                {
                    b.HasOne("TourEd.Lib.Abstractions.Models.User", null)
                        .WithMany("VisitedStampingPoints")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.HikingTour", b =>
                {
                    b.Navigation("StampingPoints");
                });

            modelBuilder.Entity("TourEd.Lib.Abstractions.Models.User", b =>
                {
                    b.Navigation("VisitedStampingPoints");
                });
#pragma warning restore 612, 618
        }
    }
}
